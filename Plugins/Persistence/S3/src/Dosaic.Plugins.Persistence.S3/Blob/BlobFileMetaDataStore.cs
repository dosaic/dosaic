using Dosaic.Hosting.Abstractions.Extensions;

namespace Dosaic.Plugins.Persistence.S3.Blob
{
    public sealed class BlobFileMetaDataStore
    {
        private readonly Dictionary<string, string> _encoded = new();

        public string? Get(string key)
        {
            var encKey = key.ToUrlEncoded();
            return _encoded.TryGetValue(encKey, out var encValue)
                ? encValue.FromUrlEncoded()
                : null;
        }

        public void Set(string key, string value)
        {
            _encoded[key.ToUrlEncoded()] = value.ToUrlEncoded();
        }

        public void Set(KeyValuePair<string, string> metadata) => Set(metadata.Key, metadata.Value);

        public void Set(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            foreach (var entry in metadata)
            {
                Set(entry.Key, entry.Value);
            }
        }

        public bool TryGetValue(string key, out string? value)
        {
            var encKey = key.ToUrlEncoded();
            if (_encoded.TryGetValue(encKey, out var encValue))
            {
                value = encValue.FromUrlEncoded();
                return true;
            }

            value = null;
            return false;
        }

        public string this[string key]
        {
            get => Get(key);
            set
            {
                if (value is null)
                    _encoded.Remove(key.ToUrlEncoded());
                else
                    Set(key, value);
            }
        }

        internal IDictionary<string, string> GetEncodedMetadata() =>
            new Dictionary<string, string>(_encoded);

        public IDictionary<string, string> GetMetadata() =>
            _encoded.ToDictionary(kv => kv.Key.FromUrlEncoded(), kv => kv.Value.FromUrlEncoded());
    }
}
