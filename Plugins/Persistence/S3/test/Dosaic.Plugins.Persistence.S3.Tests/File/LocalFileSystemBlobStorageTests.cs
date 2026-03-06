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

    [Test]
    public async Task ListObjectsAsyncReturnsUploadedFiles()
    {
        var bucket = "listbucket2";
        await _storage.CreateBucketAsync(bucket);
        for (var i = 0; i < 3; i++)
        {
            using var s = new MemoryStream([(byte)i]);
            await _storage.SetAsync(new BlobFile(new FileId(bucket, $"file{i}.txt")), s, FileType.Any);
        }

        var result = new List<FileListItem>();
        await foreach (var item in _storage.ListObjectsAsync(bucket, new ListObjectOptions()))
            result.Add(item);

        result.Should().HaveCount(3);
        result.Select(x => x.FileId.Key).Should().BeEquivalentTo(["file0.txt", "file1.txt", "file2.txt"]);
        result.All(x => x.FileId.Bucket == bucket).Should().BeTrue();
        result.All(x => !x.IsDir).Should().BeTrue();
    }

    [Test]
    public async Task ListObjectsAsyncWithPrefixFiltersResults()
    {
        var bucket = "prefixbucket";
        await _storage.CreateBucketAsync(bucket);
        using var s1 = new MemoryStream([1]);
        await _storage.SetAsync(new BlobFile(new FileId(bucket, "docs/a.txt")), s1, FileType.Any);
        using var s2 = new MemoryStream([2]);
        await _storage.SetAsync(new BlobFile(new FileId(bucket, "docs/b.txt")), s2, FileType.Any);
        using var s3 = new MemoryStream([3]);
        await _storage.SetAsync(new BlobFile(new FileId(bucket, "imgs/c.png")), s3, FileType.Any);

        var result = new List<FileListItem>();
        await foreach (var item in _storage.ListObjectsAsync(bucket, new ListObjectOptions { Prefix = "docs/", Recursive = true }))
            result.Add(item);

        result.Should().HaveCount(2);
        result.All(x => x.FileId.Key.StartsWith("docs/")).Should().BeTrue();
    }

    [Test]
    public async Task ListObjectsAsyncWithRecursiveFindsNestedFiles()
    {
        var bucket = "recursbucket";
        await _storage.CreateBucketAsync(bucket);
        using var s1 = new MemoryStream([1]);
        await _storage.SetAsync(new BlobFile(new FileId(bucket, "root.txt")), s1, FileType.Any);
        using var s2 = new MemoryStream([2]);
        await _storage.SetAsync(new BlobFile(new FileId(bucket, "sub/nested.txt")), s2, FileType.Any);

        var recursive = new List<FileListItem>();
        await foreach (var item in _storage.ListObjectsAsync(bucket, new ListObjectOptions { Recursive = true }))
            recursive.Add(item);
        recursive.Should().HaveCount(2);

        var nonRecursive = new List<FileListItem>();
        await foreach (var item in _storage.ListObjectsAsync(bucket, new ListObjectOptions { Recursive = false }))
            nonRecursive.Add(item);
        nonRecursive.Should().HaveCount(1);
        nonRecursive[0].FileId.Key.Should().Be("root.txt");
    }

    [Test]
    public async Task ListObjectsAsyncOnNonExistentBucketReturnsEmpty()
    {
        var result = new List<FileListItem>();
        await foreach (var item in _storage.ListObjectsAsync("nonexistent", new ListObjectOptions()))
            result.Add(item);
        result.Should().BeEmpty();
    }
}
