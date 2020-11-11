using LibPackageManager.Provider;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LibPackageManager.Database
{
    /// <summary>
    /// Provides an abstract database class for managing, saving, loading and fetching a list of IRepository.
    /// </summary>
    /// <typeparam name="T">The IRepositoryItem-implementing class this database manages.</typeparam>
    public abstract class BaseDatabaseManager<T>
        where T : class, IRepositoryItem
    {
        #region Variables
        protected string dbFilePath;
        protected List<IRepository<T>> repositories;
        #endregion

        #region Properties
        /// <summary>
        /// The items currently present in the database.
        /// </summary>
        public List<T> Items { get; protected set; } = new();

        /// <summary>
        /// Indicates if the database data has been loaded.
        /// </summary>
        public bool IsLoaded { get; protected set; } = false;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new BaseDatabaseManager using the specified database file and repositories.
        /// </summary>
        /// <param name="dbFilePath">Path of the database file.</param>
        /// <param name="repositories">Providers to read item data from.</param>
        public BaseDatabaseManager(string dbFilePath, List<IRepository<T>> repositories)
        {
            // Initialise and perform null argument check
            this.dbFilePath = dbFilePath ?? throw new ArgumentNullException(nameof(dbFilePath));
            this.repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
        }
        #endregion

        #region Methods
        /// <summary>
        /// Fetches an item by its ID.
        /// </summary>
        /// <param name="id">ID of the object to be found.</param>
        /// <returns>The found database item, or null if no such item exists.</returns>
        public T this[string id]
        {
            get
            {
                try
                {
                    return Items.First(x => x.Id == id);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Performs an initial load of the database, either from the database file or by refreshing provider data.
        /// </summary>
        public async Task LoadDatabaseAsync()
        {
            if (!File.Exists(dbFilePath))
            {
                await RefreshDatabaseAsync();
            }
            else
            {
                await DeserialiseDatabaseAsync();
            }

            IsLoaded = true;
        }

        /// <summary>
        /// Saves the database to the database file.
        /// </summary>
        public async Task SaveDatabaseAsync()
        {
            await SerialiseDatabaseAsync();
        }

        /// <summary>
        /// Refreshes the data in Items by refreshing data in each provider and merging.
        /// </summary>
        public async Task RefreshDatabaseAsync()
        {
            Items.Clear();

            // Run all provider fetch tasks simultaneously
            List<Task> providerRefreshTasks = new();
            foreach (IRepository<T> provider in repositories)
            {
                providerRefreshTasks.Add(provider.RefreshAsync());
            }
            await Task.WhenAll(providerRefreshTasks);

            // Merge items together to populate Items.
            MergeItemLists();

            // Populate dependency list
            PopulateDependencies();

            // Serialise resulting list
            await SerialiseDatabaseAsync();
        }

        /// <summary>
        /// Merges all the item lists from the providers together and stores the result in Items.
        /// </summary>
        protected void MergeItemLists()
        {
            // Copy first provider's items into Items
            Items = repositories[0].Items.ToList();

            // Merge each subsequent provider's items into Items
            for (int i = 1; i < repositories.Count; i++)
            {
                IRepository<T> provider = repositories[i];

                foreach (T superior in provider.Items)
                {
                    bool wasMerged = false;

                    for (int j = 0; j < Items.Count; j++)
                    {
                        T inferior = Items[j];

                        if (superior.Id == inferior.Id)
                        {
                            Items[j] = MergeItems(superior, inferior);

                            wasMerged = true;
                        }
                    }

                    if (!wasMerged)
                    {
                        Items.Add(superior);
                    }
                }
            }
        }

        /// <summary>
        /// Merges two items together, with values from superior overriding those of inferior unless the value in superior is null.
        /// </summary>
        /// <param name="superior">The item whose values take precedence.</param>
        /// <param name="inferior">The item whose values are overwritten, unless the corresponding value in superior is null.</param>
        /// <returns>The result of merging the two items.</returns>
        protected abstract T MergeItems(T superior, T inferior);

        /// <summary>
        /// Populates the dependency list of each item.
        /// </summary>
        protected void PopulateDependencies()
        {
            if (typeof(IDependentRepositoryItem).IsAssignableFrom(typeof(T)))
            {
                List<IDependentRepositoryItem> dependentItems = Items.Cast<IDependentRepositoryItem>().ToList();
                foreach (IDependentRepositoryItem item in dependentItems)
                {
                    if (item.Dependencies is null) continue;
                    List<string> keys = item.Dependencies.Keys.ToList();

                    foreach (string key in keys)
                    {
                        item.Dependencies[key] = this[key] as IDependentRepositoryItem;
                    }
                }
            }
        }

        /// <summary>
        /// Serialises the database into a JSON file.
        /// </summary>
        protected async Task SerialiseDatabaseAsync()
        {
            await Task.Run(() => File.WriteAllText(dbFilePath, JsonConvert.SerializeObject(Items, Formatting.Indented)));
        }

        /// <summary>
        /// Deserialises the database from a JSON file and stores the result in Items.
        /// </summary>
        protected async Task DeserialiseDatabaseAsync()
        {
            Items = await Task.Run(() => JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(dbFilePath)));

            PopulateDependencies();
        }
        #endregion
    }
}
