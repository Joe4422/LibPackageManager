using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibPackageManager.Repositories
{
    /// <summary>
    /// Implements a dictionary of IRepositoryItem members this item is dependent on to function.
    /// </summary>
    public interface IDependentRepositoryItem : IRepositoryItem
    {
        /// <summary>
        /// List of items that this item relies on.
        /// Key:    ID
        /// Value:  Value this ID corresponds to, or null if none was found.
        /// </summary>
        Dictionary<string, IDependentRepositoryItem> Dependencies { get; }
    }
}
