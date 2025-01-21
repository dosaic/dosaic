using Microsoft.AspNetCore.Http;

namespace Dosaic.Hosting.Abstractions.Exceptions
{
    public class ConflictDosaicException : DosaicException
    {
        public ConflictDosaicException(string message) : base(message)
        {
            HttpStatus = StatusCodes.Status409Conflict;
        }
    }
}
