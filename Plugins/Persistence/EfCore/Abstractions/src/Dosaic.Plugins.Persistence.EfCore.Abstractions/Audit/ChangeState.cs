using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit
{
    /// <summary>
    /// The state of a model
    /// </summary>
    [DbEnum("change_state", "common")]
    public enum ChangeState
    {
        /// <summary>
        /// Undefined state
        /// </summary>
        None = 0,

        /// <summary>
        /// The model was added
        /// </summary>
        Added = 1,

        /// <summary>
        /// The model was modified
        /// </summary>
        Modified = 2,

        /// <summary>
        /// The model was deleted
        /// </summary>
        Deleted = 3
    }
}
