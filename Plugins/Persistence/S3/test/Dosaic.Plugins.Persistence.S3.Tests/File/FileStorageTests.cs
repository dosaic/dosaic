using System.Collections.Immutable;
using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Dosaic.Hosting.Abstractions.Exceptions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using Dosaic.Testing.NUnit.Assertions;
using Microsoft.AspNetCore.Http;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using FileType = Dosaic.Plugins.Persistence.S3.File.FileType;

namespace Dosaic.Plugins.Persistence.S3.Tests.File
{
    public class FileStorageTests
    {
        private IAmazonS3 _s3Client;
        private IContentInspector _contentInspector;
        private IFileStorage<SampleBucket> _fileStorageSampleBucket;
        private IFileStorage _fileStorage;
        private S3Configuration _configuration;

        private readonly TestFileTypeDefinitionResolver _testFileTypeDefinitionResolver = new();

        private static readonly byte[] _imageSignature = [0xFF, 0xD8, 0xFF, 0x00];
        private static readonly byte[] _applicationOctetSignature = [0x00];
        private static readonly byte[] _pdfSignature = "%PDF"u8.ToArray();

        [SetUp]
        public void Setup()
        {
            _s3Client = Substitute.For<IAmazonS3>();
            _contentInspector = new ContentInspectorBuilder
            {
                Definitions =
                    [.. DefaultDefinitions.FileTypes.Images.JPEG(), .. DefaultDefinitions.FileTypes.Documents.PDF()]
            }.Build();
            _configuration = new S3Configuration { BucketPrefix = "dev-" };
            _fileStorage = new FileStorage(_s3Client, _contentInspector,
                new FakeLogger<FileStorage>(), _configuration, _testFileTypeDefinitionResolver);
            _fileStorageSampleBucket = new FileStorage<SampleBucket>(_fileStorage);
        }

        [TearDown]
        public void TearDown()
        {
            _s3Client.Dispose();
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

        private static GetObjectMetadataResponse GetObjectMetadata(string etag = null, DateTime? lastModified = null,
            string filename = null, string other = null)
        {
            var response = new GetObjectMetadataResponse
            {
                ETag = etag ?? "",
                LastModified = lastModified ?? DateTime.UtcNow,
                ContentLength = 42
            };
            response.Headers.ContentType = "application/pdf";
            response.Metadata.Add("other", other ?? "test");
            response.Metadata.Add(BlobFileMetaData.Filename, filename ?? "test.pdf");
            response.Metadata.Add(BlobFileMetaData.Hash, "file-hash");
            return response;
        }

        private static PutObjectResponse OkPutResponse() => new() { HttpStatusCode = HttpStatusCode.OK };

        [Test]
        public async Task GetAsyncWorks()
        {
            var lastModified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(GetObjectMetadata("etag", lastModified, null, "OTHER"));
            var result = await _fileStorageSampleBucket.GetFileAsync(GetId("123"));
            result.MetaData[BlobFileMetaData.Filename].Should().Be("test.pdf");
            result.MetaData[BlobFileMetaData.ETag].Should().Be("\"etag\"");
            result.MetaData[BlobFileMetaData.ContentType].Should().Be("application/pdf");
            result.MetaData[BlobFileMetaData.ContentLength].Should().Be("42");
            result.MetaData[BlobFileMetaData.Hash].Should().Be("file-hash");
            result.MetaData.GetMetadata().Should().HaveCount(5);
            result.LastModified.Should().Be(new DateTimeOffset(lastModified));
        }

        [Test]
        public async Task GetAsyncResolvesPrefixedBucketAndKey()
        {
            _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(GetObjectMetadata("etag"));
            await _fileStorageSampleBucket.GetFileAsync(GetId("123"));
            await _s3Client.Received(1).GetObjectMetadataAsync(
                Arg.Is<GetObjectMetadataRequest>(r => r.BucketName == "dev-test-logos" && r.Key == "123"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GetAsyncWorksWithRealFilename()
        {
            _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(GetObjectMetadata("etag", null, "inv.pdf", "OTHER"));
            var result = await _fileStorageSampleBucket.GetFileAsync(GetId("123"));
            result.MetaData[BlobFileMetaData.Filename].Should().Be("inv.pdf");
            result.MetaData[BlobFileMetaData.ETag].Should().Be("\"etag\"");
            result.MetaData[BlobFileMetaData.Hash].Should().Be("file-hash");
            result.MetaData.GetMetadata().Should().HaveCount(5);
        }

        [Test]
        public async Task GetAsyncFallsBackToKeyWhenFilenameMetadataIsMissing()
        {
            var response = new GetObjectMetadataResponse { ETag = "etag", LastModified = DateTime.UtcNow };
            response.Headers.ContentType = "application/pdf";
            _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(response);
            var result = await _fileStorageSampleBucket.GetFileAsync(GetId("123"));
            result.MetaData[BlobFileMetaData.Filename].Should().Be("123");
            result.MetaData.GetMetadata().Should().HaveCount(4);
        }

        [Test]
        public async Task GetAsyncPropagatesExceptions()
        {
            _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Throws(new Exception("test"));
            await _fileStorageSampleBucket.Invoking(async x => await x.GetFileAsync(GetId("123"))).Should()
                .ThrowAsync<Exception>();
        }

        [Test]
        public async Task ConsumeAsyncWorks()
        {
            _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectResponse { ResponseStream = new MemoryStream("test"u8.ToArray()) });

            using var stream = new MemoryStream();
            await _fileStorageSampleBucket.ConsumeStreamAsync(GetId("123"),
                (stream1, token) => stream1.CopyToAsync(stream, token));

            await _s3Client.Received(1).GetObjectAsync(
                Arg.Is<GetObjectRequest>(r => r.BucketName == "dev-test-logos" && r.Key == "123"),
                Arg.Any<CancellationToken>());

            stream.Seek(0, SeekOrigin.Begin);
            using var sr = new StreamReader(stream);
            var content = await sr.ReadToEndAsync();
            content.Should().Be("test");
        }

        [Test]
        public async Task SetWithFilenameAsyncWorks()
        {
            _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(OkPutResponse());
            await using var imageStream = CreateStream("test", _imageSignature);
            var blob = new BlobFile<SampleBucket>(SampleBucket.Logos, "test");
            blob.MetaData.Set(new Dictionary<string, string>
            {
                { BlobFileMetaData.Filename, "test.pdf" }, { "something-custom", "test" }
            });
            var result = await _fileStorageSampleBucket.SetAsync(
                blob,
                imageStream);
            result.Bucket.Should().Be(SampleBucket.Logos);
            result.Key.Should().Be("test");

            var request = _s3Client.ReceivedCalls().Last().GetArguments().OfType<PutObjectRequest>().First();
            request.Should().NotBeNull();
            request.BucketName.Should().Be($"{_configuration.BucketPrefix}{SampleBucket.Logos.GetName()}");
            request.ContentType.Should().Be("image/jpeg");
            request.Key.Should().Be("test");
            request.AutoCloseStream.Should().BeFalse();
            request.Metadata[BlobFileMetaData.Filename].Should().Be("test.pdf");
            request.Metadata["something-custom"].Should().Be("test");
            request.Metadata[BlobFileMetaData.Hash].Should().NotBeNullOrWhiteSpace();
            request.InputStream.Should().BeSameAs(imageStream);
            imageStream.Seek(0, SeekOrigin.Begin);
            using var sr = new StreamReader(imageStream);
            (await sr.ReadToEndAsync()).Should().EndWith("test");
        }

        [Test]
        public async Task SetWithFileExtensionAsyncWorks()
        {
            _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(OkPutResponse());
            await using var imageStream = CreateStream("test", _imageSignature);
            var result = await _fileStorageSampleBucket.SetAsync(
                new BlobFile<SampleBucket>(SampleBucket.Logos, "test").WithFileExtension(".jpg"),
                imageStream);
            result.Bucket.Should().Be(SampleBucket.Logos);
            result.Key.Should().Be("test");

            var request = _s3Client.ReceivedCalls().Last().GetArguments().OfType<PutObjectRequest>().First();
            request.Should().NotBeNull();
            request.Metadata[BlobFileMetaData.FileExtension].Should().Be(".jpg");
            request.Metadata[BlobFileMetaData.Hash].Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public async Task SetAsyncThrowsUnhandledOnNoResult()
        {
            await using var imgStream = CreateStream("test", _imageSignature);
            var ex = (await _fileStorageSampleBucket
                .Invoking(async x => await x.SetAsync(
                    new BlobFile<SampleBucket>(SampleBucket.Logos, "test"),
                    // ReSharper disable once AccessToDisposedClosure
                    imgStream)).Should().ThrowAsync<DosaicException>()).Subject.First();
            ex.HttpStatus.Should()
                .Be(StatusCodes.Status500InternalServerError);
            ex.Message.Should().Be("Could not save file dev-test-logos:test to s3");
        }

        [Test]
        public async Task SetAsyncThrowsUnhandledOnErrorStatusCode()
        {
            _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse { HttpStatusCode = HttpStatusCode.Forbidden });
            await using var imgStream = CreateStream("test", _imageSignature);
            var ex = (await _fileStorageSampleBucket
                .Invoking(async x => await x.SetAsync(
                    new BlobFile<SampleBucket>(SampleBucket.Logos, "test"),
                    // ReSharper disable once AccessToDisposedClosure
                    imgStream)).Should().ThrowAsync<DosaicException>()).Subject.First();
            ex.Message.Should().Be("Could not save file dev-test-logos:test to s3");
        }

        [Test]
        public async Task SetAsyncThrowsValidationOnInvalidMimeType()
        {
            await using var pdfStream = CreateStream("test", _pdfSignature);
            var ex = (await _fileStorageSampleBucket
                .Invoking(async x => await x.SetAsync(
                    new BlobFile<SampleBucket>(SampleBucket.Logos, "test"),
                    // ReSharper disable once AccessToDisposedClosure
                    pdfStream)).Should().ThrowAsync<ValidationDosaicException>()).Subject.First();
            ex.HttpStatus.Should()
                .Be(StatusCodes.Status400BadRequest);
            ex.Message.Should()
                .Be(
                    "Cannot validate BlobFile. Invalid file format. Only image/bmp,image/gif,image/x-icon,image/jpeg,image/png,image/tiff,image/tiff,image/tiff,image/tiff,image/webp allowed!");
        }

        [Test]
        public async Task SetAsyncWithApplicatoinOctetStreamThrowsValidationOnInvalidMimeType()
        {
            await using var pdfStream = CreateStream("test", _applicationOctetSignature);
            var ex = (await _fileStorageSampleBucket
                .Invoking(async x => await x.SetAsync(
                    new BlobFile<SampleBucket>(SampleBucket.Logos, "test"),
                    // ReSharper disable once AccessToDisposedClosure
                    pdfStream)).Should().ThrowAsync<ValidationDosaicException>()).Subject.First();
            ex.HttpStatus.Should()
                .Be(StatusCodes.Status400BadRequest);
            ex.Message.Should()
                .Be(
                    "Cannot validate BlobFile. Invalid file format. Only image/bmp,image/gif,image/x-icon,image/jpeg,image/png,image/tiff,image/tiff,image/tiff,image/tiff,image/webp allowed!");
        }

        [Test]
        public async Task DeleteAsyncWorks()
        {
            var action = async () => await _fileStorageSampleBucket.DeleteFileAsync(GetId("123"));
            await action.Should().NotThrowAsync();
            await _s3Client.Received(1)
                .DeleteObjectAsync(
                    Arg.Is<DeleteObjectRequest>(r => r.BucketName == "dev-test-logos" && r.Key == "123"),
                    Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SkipDeleteAsyncWorks()
        {
            _configuration = new S3Configuration { BucketPrefix = "dev-", SkipFileDeletion = true };
            _fileStorage = new FileStorage(_s3Client, _contentInspector,
                new FakeLogger<FileStorage>(), _configuration, _testFileTypeDefinitionResolver);
            _fileStorageSampleBucket = new FileStorage<SampleBucket>(_fileStorage);
            var action = async () => await _fileStorageSampleBucket.DeleteFileAsync(GetId("123"));
            await action.Should().NotThrowAsync();
            await _s3Client.Received(0)
                .DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CreateBucketAsyncWorks()
        {
            var action = async () => await _fileStorage.CreateBucketAsync("test-bucket");
            await action.Should().NotThrowAsync();
            await _s3Client.Received(1)
                .PutBucketAsync(Arg.Is<PutBucketRequest>(r => r.BucketName == "dev-test-bucket"),
                    Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SampleBucketComputeHashWorks()
        {
            var bytes = "test"u8.ToArray();
            var hash = await _fileStorageSampleBucket.ComputeHash(bytes);
            hash.Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
            var stream = new MemoryStream(bytes);
            hash = await _fileStorageSampleBucket.ComputeHash(stream);
            hash.Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
        }

        [Test]
        public async Task ComputeHashWorks()
        {
            var bytes = "test"u8.ToArray();
            var hash = await _fileStorage.ComputeHash(bytes);
            hash.Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
            var stream = new MemoryStream(bytes);
            hash = await _fileStorage.ComputeHash(stream);
            hash.Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
        }

        [Test, Parallelizable(ParallelScope.None)]
        public async Task ComputeHashWorksIsThreadSafe()
        {
            var bytes = Encoding.UTF8.GetBytes(new string('t', 10_000));
            var tasks = new System.Collections.Concurrent.ConcurrentBag<Task<string>>();

            Parallel.For(0, 1000, _ => { tasks.Add(_fileStorage.ComputeHash(bytes)); });

            var results = await Task.WhenAll(tasks);
            results.Should().AllSatisfy(r => r.Should().NotBeEmpty());
        }

        [Test]
        public async Task ListObjectsAsyncReturnsItems()
        {
            var lastModified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            _s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response
                {
                    S3Objects =
                    [
                        new S3Object
                        {
                            Key = "logo.png", ETag = "\"etag1\"", Size = 2048, LastModified = lastModified
                        }
                    ],
                    CommonPrefixes = ["sub/"]
                });

            var result = new List<FileListItem>();
            await foreach (var item in _fileStorage.ListObjectsAsync("test-logos", new ListObjectOptions()))
                result.Add(item);

            result.Should().HaveCount(2);
            result[0].FileId.Key.Should().Be("sub/");
            result[0].IsDir.Should().BeTrue();
            result[1].FileId.Key.Should().Be("logo.png");
            result[1].FileId.Bucket.Should().Be("test-logos");
            result[1].ETag.Should().Be("etag1");
            result[1].Size.Should().Be(2048);
            result[1].LastModified.Should().Be(new DateTimeOffset(lastModified));
            result[1].IsDir.Should().BeFalse();
        }

        [Test]
        public async Task ListObjectsAsyncFollowsContinuationToken()
        {
            _s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(
                    _ => new ListObjectsV2Response
                    {
                        S3Objects = [new S3Object { Key = "a" }],
                        NextContinuationToken = "token"
                    },
                    _ => new ListObjectsV2Response { S3Objects = [new S3Object { Key = "b" }] });

            var result = new List<FileListItem>();
            await foreach (var item in _fileStorage.ListObjectsAsync("test-logos"))
                result.Add(item);

            result.Select(x => x.FileId.Key).Should().Equal("a", "b");
            await _s3Client.Received(2)
                .ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ListObjectsAsyncPassesOptionsToS3()
        {
            _s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response());

            await foreach (var _ in _fileStorage.ListObjectsAsync("test-logos",
                               new ListObjectOptions { Prefix = "docs/", Recursive = true })) { }

            await _s3Client.Received(1).ListObjectsV2Async(
                Arg.Is<ListObjectsV2Request>(r =>
                    r.BucketName == "dev-test-logos" && r.Prefix == "docs/" && r.Delimiter == null),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ListObjectsAsyncWithDefaultOptionsPassesNoPrefixAndDelimiter()
        {
            _s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response());

            await foreach (var _ in _fileStorage.ListObjectsAsync("test-logos")) { }

            await _s3Client.Received(1).ListObjectsV2Async(
                Arg.Is<ListObjectsV2Request>(r => r.Prefix == null && r.Delimiter == "/"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ListObjectsAsyncTypedMapsItemsToBucketEnum()
        {
            _s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response
                {
                    S3Objects = [new S3Object { Key = "logo.png", ETag = "\"img-etag\"", Size = 512 }]
                });

            var result = new List<FileListItem<SampleBucket>>();
            await foreach (var i in _fileStorageSampleBucket.ListObjectsAsync(SampleBucket.Logos,
                               new ListObjectOptions()))
                result.Add(i);

            result.Should().HaveCount(1);
            result[0].FileId.Bucket.Should().Be(SampleBucket.Logos);
            result[0].FileId.Key.Should().Be("logo.png");
            result[0].ETag.Should().Be("img-etag");
            result[0].Size.Should().Be(512);
            result[0].IsDir.Should().BeFalse();
        }

        [Test]
        public void GetDefinitionsForAllFileTypeReturnsAllDefinitions()
        {
            var defs = ((FileStorage)_fileStorage).GetDefinitions(FileType.All);

            defs.Should().NotBeEmpty();
            defs.Should().BeEquivalentTo(DefaultDefinitions.All());
        }

        [Test]
        public void GetDefinitionsForSpecificFileTypeReturnsMatchingDefinitions()
        {
            var defs = ((FileStorage)_fileStorage).GetDefinitions(FileType.Images);

            defs.Should().NotBeEmpty();
            defs.Should().BeEquivalentTo([
                .. DefaultDefinitions.FileTypes.Images.All().Where(x => !x.File.Extensions.Contains("psd"))
            ]);
        }

        [Test]
        public void GetDefinitionsForNoneFileTypeReturnsEmptyCollection()
        {
            var defs = ((FileStorage)_fileStorage).GetDefinitions(FileType.Any);

            defs.Should().BeEmpty();
        }

        [Test]
        public void GetDefinitionsForMultipleFileTypesReturnsCombinedDefinitions()
        {
            var combinedType = FileType.Xml | FileType.Documents;
            var defs = ((FileStorage)_fileStorage).GetDefinitions(combinedType);

            var expectedDefs = new List<Definition>();
            expectedDefs.AddRange(DefaultDefinitions.FileTypes.Xml.All());
            expectedDefs.AddRange(DefaultDefinitions.FileTypes.Documents.All());

            defs.Should().NotBeEmpty();
            defs.Should().BeEquivalentTo(expectedDefs);
        }

        [Test]
        public void GetDefinitionsForBucketReturnsDefinitionsMatchingBucketFileType()
        {
            var fileTypeDefs = ((FileStorage)_fileStorage).GetDefinitions(SampleBucket.Logos.GetFileType());

            fileTypeDefs.Should().BeEquivalentTo([
                .. DefaultDefinitions.FileTypes.Images.All().Where(x => !x.File.Extensions.Contains("psd"))
            ]);
        }
    }

    internal class TestFileTypeDefinitionResolver : IFileTypeDefinitionResolver
    {
        public ImmutableArray<Definition> GetDefinitions(FileType fileType)
        {
            return fileType switch
            {
                FileType.Any => [],
                FileType.Archives => DefaultDefinitions.FileTypes.Archives.All(),
                FileType.Documents => DefaultDefinitions.FileTypes.Documents.All(),
                FileType.Email => DefaultDefinitions.FileTypes.Email.All(),
                FileType.Images =>
                    [.. DefaultDefinitions.FileTypes.Images.All().Where(x => !x.File.Extensions.Contains("psd"))],
                FileType.Text => DefaultDefinitions.FileTypes.Text.All(),
                FileType.Xml => DefaultDefinitions.FileTypes.Xml.All(),
                _ => DefaultDefinitions.All()
            };
        }
    }
}
