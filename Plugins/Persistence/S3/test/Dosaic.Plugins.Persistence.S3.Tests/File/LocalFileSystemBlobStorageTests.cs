using AwesomeAssertions;
using Dosaic.Plugins.Persistence.S3.Blob;
using Dosaic.Plugins.Persistence.S3.File;
using NUnit.Framework;

namespace Dosaic.Plugins.Persistence.S3.Tests.File;

[TestFixture]
public class LocalFileSystemBlobStorageTests
{
    private string _tempPath;
    private LocalFileSystemBlobStorage _storage;

    [SetUp]
    public void SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _storage = new LocalFileSystemBlobStorage(_tempPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
    }

    [Test]
    public async Task UploadAndDownloadFile()
    {
        var id = new FileId("testbucket", "testkey");
        var content = "hello world"u8.ToArray();
        var blob = new BlobFile(id);
        blob.MetaData.Set(BlobFileMetaData.Filename, "hello.txt");

        using var uploadStream = new MemoryStream(content);
        await _storage.SetAsync(blob, uploadStream, FileType.Any);

        var retrievedBlob = await _storage.GetFileAsync(id);
        retrievedBlob.Should().NotBeNull();
        retrievedBlob.Id.Bucket.Should().Be("testbucket");
        retrievedBlob.Id.Key.Should().Be("testkey");
        retrievedBlob.MetaData.Get(BlobFileMetaData.Filename).Should().Be("hello.txt");
    }

    [Test]
    public async Task ConsumeStreamReadsBlobBytes()
    {
        var id = new FileId("bucket0", "key0");
        var content = "stream content"u8.ToArray();
        var blob = new BlobFile(id);
        using var uploadStream = new MemoryStream(content);
        await _storage.SetAsync(blob, uploadStream, FileType.Any);

        var received = new MemoryStream();
        await _storage.ConsumeStreamAsync(id, async (s, ct) => await s.CopyToAsync(received, ct));
        received.ToArray().Should().BeEquivalentTo(content);
    }

    [Test]
    public async Task DeleteFileRemovesFileAndMetadata()
    {
        var id = new FileId("delbucket", "delkey");
        var blob = new BlobFile(id);
        using var stream = new MemoryStream("delete me"u8.ToArray());
        await _storage.SetAsync(blob, stream, FileType.Any);

        await _storage.DeleteFileAsync(id);

        var act = async () => await _storage.GetFileAsync(id);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public async Task ListBucketFilesAfterUpload()
    {
        var bucket = "listbucket";
        await _storage.CreateBucketAsync(bucket);
        for (var i = 0; i < 3; i++)
        {
            var fid = new FileId(bucket, $"file{i}");
            var fb = new BlobFile(fid);
            using var s = new MemoryStream([(byte)i]);
            await _storage.SetAsync(fb, s, FileType.Any);
        }

        var files = Directory.EnumerateFiles(Path.Combine(_tempPath, bucket), "*",
            SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".meta.json"))
            .ToList();
        files.Should().HaveCount(3);
    }

    [Test]
    public async Task HashIsStoredInMetadata()
    {
        var id = new FileId("hashbucket", "hashkey");
        var content = "data"u8.ToArray();
        var blob = new BlobFile(id);
        using var stream = new MemoryStream(content);
        await _storage.SetAsync(blob, stream, FileType.Any);

        var retrieved = await _storage.GetFileAsync(id);
        retrieved.MetaData.Get(BlobFileMetaData.Hash).Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ComputeHashResetsStreamPosition()
    {
        var content = "some bytes"u8.ToArray();
        using var stream = new MemoryStream(content);
        var hash = await _storage.ComputeHash(stream, CancellationToken.None);
        stream.Position.Should().Be(0);
        hash.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ResolveBucketNameReturnsBucketAsIs()
    {
        _storage.ResolveBucketName("my-bucket").Should().Be("my-bucket");
    }

    [Test]
    public async Task CreateBucketCreatesDirectory()
    {
        await _storage.CreateBucketAsync("newbucket");
        Directory.Exists(Path.Combine(_tempPath, "newbucket")).Should().BeTrue();
    }
}
