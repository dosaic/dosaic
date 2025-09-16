using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public static class ModelExtensions
    {
        public static void PatchModel<T>(this T model, T values, PatchMode patchMode = PatchMode.Full) where T : class, IModel
        {
            model.DeepPatch(values, patchMode,
                (_, p) => p.Name != nameof(IModel.Id) && !p.PropertyType.Implements(typeof(IModel)));
        }
    }
}
