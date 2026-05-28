
using System.Diagnostics.CodeAnalysis;
using Dosaic.Extensions.Localization;
using Dosaic.Hosting.Abstractions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vogen;

namespace Dosaic.Example.Service
{
    [ApiController, Route("test")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public Entry Get()
        {
            return new Entry() { Source = "test" };
        }

        [HttpDelete]
        public Entry Delete()
        {
            return new Entry() { Source = "test" };
        }

        /// <summary>
        /// Create some temp resource
        /// </summary>
        /// <param name="entry">the entry to manipulate the id</param>
        /// <param name="idToSet">the id to set</param>
        /// <returns>the same object, with the id 123</returns>
        [HttpPost]
        [SwaggerResponse(200, "the manipulated object", typeof(Entry))]
        public Entry Create([FromBody] Entry entry, [FromQuery] EntryId idToSet)
        {
            return entry;
        }

        [HttpPost("upload")]
        public ActionResult FileUpload(IFormFile file)
        {
            var stream = file.OpenReadStream();
            stream.Position = 0;
            var content = new StreamReader(stream).ReadToEnd();
            return Ok(new { content, file = file }.Serialize());
        }
    }

    /// <summary>
    /// The value object
    /// </summary>
    [ValueObject<int>]
    public partial class EntryId
    {
        private static Validation Validate(int input) => input < 1 ? Validation.Invalid("lower as one") : Validation.Ok;
    }

    public class Entry
    {

        /// <summary>
        /// The source
        /// </summary>
        [NotNull]
        [LocalizedName(de: "Quelle", en: "Source")]
        public string Source { get; set; }

        public Guid Id { get; set; }
        public Guid NewId() => Guid.NewGuid();
    }
}
