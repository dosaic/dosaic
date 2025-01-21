using Microsoft.AspNetCore.Http;

namespace Dosaic.Hosting.Abstractions.Exceptions
{
    public class DosaicException : Exception
    {
        public DosaicException() : base("An unhandled error occured.") { }
        public DosaicException(string message) : base(message) { }
        public DosaicException(string message, Exception innerException) : base(message, innerException) { }
        public DosaicException(int httpStatus) : base("An unhandled error occured.")
        {
            HttpStatus = httpStatus;
        }
        public DosaicException(string message, int httpStatus) : base(message)
        {
            HttpStatus = httpStatus;
        }

        public DosaicException(string message, int httpStatus, Exception innerException) : base(message, innerException)
        {
            HttpStatus = httpStatus;
        }

        public int HttpStatus { get; set; } = StatusCodes.Status500InternalServerError;
    }
}
