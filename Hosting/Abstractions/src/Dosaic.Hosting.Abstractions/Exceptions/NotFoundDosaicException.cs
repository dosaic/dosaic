using Microsoft.AspNetCore.Http;

namespace Dosaic.Hosting.Abstractions.Exceptions
{
    public class NotFoundDosaicException : DosaicException
    {
        public NotFoundDosaicException(string message) : base(message)
        {
            HttpStatus = StatusCodes.Status404NotFound;
        }

        public NotFoundDosaicException() : base("not found")
        {
            HttpStatus = StatusCodes.Status404NotFound;
        }

        public NotFoundDosaicException(string entity, string id) : this($"Could not find {entity} with id '{id}'")
        {
        }
    }
}
