using System.Globalization;
using System.Net;
using System.Text;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit.Assertions;
using Dosaic.Testing.NUnit.Extensions;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.AspNetCore.Http;
using MimeDetective;
using MimeDetective.Definitions;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Response;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests
{
    public class FileStorageTests
    {
        private IMinioClient _minioClient;
        private IContentInspector _contentInspector;
        private IFileStorage<SampleBucket> _fileStorage;
        private static readonly byte[] _imageSignature = [0xFF, 0xD8, 0xFF, 0x00];
        private static readonly byte[] _pdfSignature = "%PDF"u8.ToArray();

        [SetUp]
        public void Setup()
        {
            _minioClient = Substitute.For<IMinioClient>();
            _contentInspector = new ContentInspectorBuilder
            {
                Definitions =
                    [.. DefaultDefinitions.FileTypes.Images.JPEG(), .. DefaultDefinitions.FileTypes.Documents.PDF()]
            }.Build();
            _fileStorage = new FileStorage<SampleBucket>(_minioClient, _contentInspector,
                new FakeLogger<FileStorage<SampleBucket>>());
        }

        [TearDown]
        public void TearDown()
        {
            _minioClient.Dispose();
        }

        private static Stream CreateStream(string content, byte[] signature)
        {
            var stream = new MemoryStream();
            foreach (var b in signature)
                stream.WriteByte(b);
            stream.Write(Encoding.UTF8.GetBytes(content).AsSpan());
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static FileId<SampleBucket> GetId(string id, SampleBucket bucket = SampleBucket.Logos) =>
            new(bucket, id);

        private static ObjectStat GetObjectStat(string objName, string? etag = null, string? lastModified = null,
            string? filename = null,
            string? other = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "etag", etag ?? "" },
                {
                    "last-modified",
                    lastModified ?? DateTime.UtcNow.ToDateTimeOffset().ToString(CultureInfo.InvariantCulture)
                },
                { "x-amz-meta-other", other ?? "test" },
                { "x-amz-meta-original-filename", filename ?? "test.pdf" },
                { "x-amz-meta-hash", "file-hash" },
            };
            return ObjectStat.FromResponseHeaders(objName, headers);
        }

        [Test]
        public async Task GetAsyncWorks()
        {
            var lastModified = DateTime.UtcNow;
            _minioClient.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>())
                .Returns(GetObjectStat("testObj", "etag", lastModified.ToString(CultureInfo.InvariantCulture), null,
                    "OTHER"));
            var result = await _fileStorage.GetFileAsync(GetId("123"));
            result.MetaData[BlobFileMetaData.Filename].Should().Be("test.pdf");
            result.MetaData[BlobFileMetaData.ETag].Should().Be("\"etag\"");
            result.MetaData[BlobFileMetaData.Hash].Should().Be("file-hash");
            result.MetaData.Should().HaveCount(5);
            result.LastModified.Should().Be(DateTime.Parse(lastModified.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture));
        }

        [Test]
        public async Task GetAsyncWorksWithRealFilename()
        {
            var lastModified = DateTime.UtcNow;
            _minioClient.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>())
                .Returns(GetObjectStat("testObj", "etag", lastModified.ToString(CultureInfo.InvariantCulture), "inv.pdf",
                    "OTHER"));
            var result = await _fileStorage.GetFileAsync(GetId("123"));
            result.MetaData[BlobFileMetaData.Filename].Should().Be("inv.pdf");
            result.MetaData[BlobFileMetaData.ETag].Should().Be("\"etag\"");
            result.MetaData[BlobFileMetaData.Hash].Should().Be("file-hash");
            result.MetaData.Should().HaveCount(5);
            result.LastModified.Should().Be(DateTime.Parse(lastModified.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture));
        }

        [Test]
        public async Task GetAsyncDisposesStreamOnExceptions()
        {
            _minioClient.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>())
                .Throws(new Exception("test"));
            await _fileStorage.Invoking(async x => await x.GetFileAsync(GetId("123"))).Should().ThrowAsync<Exception>();
        }

        [Test]
        public async Task ConsumeAsyncWorks()
        {
            using var stream = new MemoryStream();
            await _fileStorage.ConsumeStreamAsync(GetId("123"), (stream1, token) => stream1.CopyToAsync(stream, token));
            var args = _minioClient.ReceivedCalls().First().GetArguments().OfType<GetObjectArgs>().First();
            var cb = args.GetInaccessibleValue<Func<Stream, CancellationToken, Task>>("CallBack");
            cb.Should().NotBeNull();
            await cb.Invoke(new MemoryStream("test"u8.ToArray()), CancellationToken.None);

            stream.Seek(0, SeekOrigin.Begin);
            using var sr = new StreamReader(stream);
            var content = await sr.ReadToEndAsync();
            content.Should().Be("test");
        }

        [Test]
        public async Task SetAsyncWorks()
        {
            _minioClient.PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse(HttpStatusCode.OK, "", new Dictionary<string, string>(), 1, ""));
            await using var imageStream = CreateStream("test", _imageSignature);
            var result = await _fileStorage.SetAsync(
                new BlobFile<SampleBucket>
                {
                    Id = new FileId<SampleBucket>(SampleBucket.Logos, "test"),
                    MetaData = new Dictionary<string, string> { { BlobFileMetaData.Filename, "test.pdf" } }
                },
                imageStream);
            result.Bucket.Should().Be(SampleBucket.Logos);
            result.Key.Should().Be("test");

            var args = _minioClient.ReceivedCalls().First().GetArguments().OfType<PutObjectArgs>().First();
            args.Should().NotBeNull();
            args.GetInaccessibleValue<string>("BucketName").Should().Be(SampleBucket.Logos.GetName());
            args.GetInaccessibleValue<string>("ContentType").Should().Be("image/jpeg");
            args.GetInaccessibleValue<string>("ObjectName").Should().Be("test");
            const string AmzKey = "x-amz-meta-" + BlobFileMetaData.Filename;
            const string HashKey = "x-amz-meta-" + BlobFileMetaData.Hash;
            args.GetInaccessibleValue<Dictionary<string, string>>("Headers").Should().ContainKey(AmzKey);
            args.GetInaccessibleValue<Dictionary<string, string>>("Headers").Should().ContainKey(HashKey);
            args.GetInaccessibleValue<Dictionary<string, string>>("Headers")[AmzKey].Should().Be("test.pdf");
            args.GetInaccessibleValue<Dictionary<string, string>>("Headers")[HashKey].Should().NotBeNullOrWhiteSpace();
            await using var data = args.GetInaccessibleValue<Stream>("ObjectStreamData");
            using var sr = new StreamReader(data);
            (await sr.ReadToEndAsync()).Should().EndWith("test");
        }

        [Test]
        public async Task SetAsyncThrowsUnhandledOnNoResult()
        {
            await using var imgStream = CreateStream("test", _imageSignature);
            var ex = (await _fileStorage
                .Invoking(async x => await x.SetAsync(
                    new BlobFile<SampleBucket> { Id = new FileId<SampleBucket>(SampleBucket.Logos, "test") },
                    // ReSharper disable once AccessToDisposedClosure
                    imgStream)).Should().ThrowAsync<DosaicException>()).Subject.First();
            ex.HttpStatus.Should()
                .Be(StatusCodes.Status500InternalServerError);
            ex.Message.Should().Be("Could not save file Logos:test to s3");
        }

        [Test]
        public async Task SetAsyncThrowsValidationOnNoMimeType()
        {
            var ex = (await _fileStorage
                    .Invoking(async x => await x.SetAsync(
                        new BlobFile<SampleBucket> { Id = new FileId<SampleBucket>(SampleBucket.Logos, "test") },
                        new MemoryStream("test"u8.ToArray()))).Should().ThrowAsync<ValidationDosaicException>()).Subject
                .First();
            ex.HttpStatus.Should()
                .Be(StatusCodes.Status400BadRequest);
            ex.Message.Should().Be("Cannot validate BlobFile`1. Could not determine content type, abort processing.");
        }

        [Test]
        public async Task SetAsyncThrowsValidationOnInvalidMimeType()
        {
            await using var pdfStream = CreateStream("test", _pdfSignature);
            var ex = (await _fileStorage
                .Invoking(async x => await x.SetAsync(
                    new BlobFile<SampleBucket> { Id = new FileId<SampleBucket>(SampleBucket.Logos, "test") },
                    // ReSharper disable once AccessToDisposedClosure
                    pdfStream)).Should().ThrowAsync<ValidationDosaicException>()).Subject.First();
            ex.HttpStatus.Should()
                .Be(StatusCodes.Status400BadRequest);
            ex.Message.Should()
                .Be(
                    "Cannot validate BlobFile`1. Invalid file format. Only image/bmp,image/gif,image/x-icon,image/jpeg,image/png,application/octet-stream,image/tiff,image/tiff,image/tiff,image/tiff,image/webp allowed!");
        }

        [Test]
        public async Task DeleteAsyncWorks()
        {
            var action = async () => await _fileStorage.DeleteFileAsync(GetId("123"));
            await action.Should().NotThrowAsync();
        }

        [Test]
        public async Task ComputeHashWorks()
        {
            var bytes = "test"u8.ToArray();
            var hash = await _fileStorage.ComputeHash(bytes);
            hash.Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
        }
    }
}
