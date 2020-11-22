using LibPackageManager.Repositories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibPackageManager.Managers
{
    /// <summary>
    /// Provides an abstract base class for downloading, installing, and uninstalling items.
    /// </summary>
    /// <typeparam name="T">The IRepositoryItem that this class supports downloading.</typeparam>
    public abstract class BaseDownloadManager<T>
        where T : IRepositoryItem
    {
        #region Variables
        protected string downloadDir;
        protected string installDir;
        protected WebClient client = new();
        #endregion

        #region Events
        public delegate void DownloadStartedEventHandler(object sender, AcquireItemJob<T> job);
        /// <summary>
        /// Indicates that a download has been initiated.
        /// </summary>
        public event DownloadStartedEventHandler DownloadJobStarted;
        #endregion

        #region Properties
        /// <summary>
        /// Contains all items that are currently being downloaded.
        /// </summary>
        public List<AcquireItemJob<T>> DownloadsInProgress { get; } = new();
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new download manager.
        /// </summary>
        /// <param name="downloadDir">The directory content should be downloaded to before installing.</param>
        /// <param name="installDir">The directory downloaded content should be installed to.</param>
        public BaseDownloadManager(string downloadDir, string installDir)
        {
            this.downloadDir = downloadDir ?? throw new ArgumentNullException(nameof(downloadDir));
            this.installDir = installDir ?? throw new ArgumentNullException(nameof(installDir));
        }
        #endregion

        #region Methods
        /// <summary>
        /// Downloads and installs an item and its dependencies.
        /// </summary>
        /// <param name="item">The item to download.</param>
        /// <returns>The DownloadJob representing the item.</returns>
        public async Task<AcquireItemJob<T>> GetItemAsync(T item)
        {
            // Check item is not null
            if (item is null) throw new ArgumentNullException(nameof(item));

            // Create download job
            AcquireItemJob<T> job = new AcquireItemJob<T>(item);
            DownloadsInProgress.Add(job);

            // Signal that download has started
            DownloadJobStarted?.Invoke(this, job);

            // Download item and dependencies recursively and sequentially
            await DownloadItemAsync(item);

            // Install items simultaneously
            Dictionary<T, Task<bool>> installTasks = new();
            foreach (T dItem in job.ItemsToAcquire)
            {
                if (dItem.Token.State == ProgressToken.ProgressState.Downloaded)
                {
                    dItem.Token.State = ProgressToken.ProgressState.InstallInProgress;
                    installTasks.Add(dItem, InstallItemAsync(dItem));
                }
            }
            await Task.WhenAll(installTasks.Values);

            // Check install task results
            foreach (var kvp in installTasks)
            {
                if (kvp.Value.Result)
                {
                    kvp.Key.Token.State = ProgressToken.ProgressState.Installed;
                }
                else
                {
                    kvp.Key.Token.State = ProgressToken.ProgressState.Failed;
                }
            }

            // Clean up downloads
            foreach (T dItem in job.ItemsToAcquire)
            {
                string downloadPath = $"{downloadDir}/{Path.GetFileName(dItem.DownloadUrl)}";

                if (File.Exists(downloadPath))
                {
                    File.Delete(downloadPath);
                }
            }

            return job;
        }

        protected async Task DownloadItemAsync(T item)
        {
            // Check for anything that would mean we wouldn't need to download the item
            if (item is null) throw new ArgumentNullException(nameof(item));
            if (item.Token.State is not ProgressToken.ProgressState.NotStarted or ProgressToken.ProgressState.Failed) return;
            if (item.DownloadUrl is null) throw new ArgumentException(nameof(item.DownloadUrl));

            // Set token state to DownloadInProgress
            item.Token.State = ProgressToken.ProgressState.DownloadInProgress;

            // Download dependencies
            foreach (T dependency in item.Dependencies.Values)
            {
                await DownloadItemAsync(dependency);
            }

            // Get download path
            string downloadPath = $"{downloadDir}/{Path.GetFileName(item.DownloadUrl)}";

            // Ensure download directory exists
            if (!Directory.Exists(downloadDir))
            {
                try
                {
                    Directory.CreateDirectory(downloadDir);
                }
                catch (Exception)
                {
                    item.Token.State = ProgressToken.ProgressState.Failed;
                    return;
                }
            }

            // Set token's client for progress updating
            item.Token.SetDownloadClient(client);

            // Attempt to download item
            try
            {
                await client.DownloadFileTaskAsync(item.DownloadUrl, downloadPath);
                item.Token.State = ProgressToken.ProgressState.Downloaded;
            }
            catch (Exception)
            {
                try
                {
                    File.Delete(downloadPath);
                }
                catch (Exception) { }
                item.Token.State = ProgressToken.ProgressState.Failed;
            }
        }

        /// <summary>
        /// Installs an item to the specified directory.
        /// </summary>
        /// <param name="token">The token representing the item to install.</param>
        protected abstract Task<bool> InstallItemAsync(T item);

        /// <summary>
        /// Uninstalls an item.
        /// </summary>
        /// <param name="item">The item to uninstall.</param>
        public async Task RemoveItemAsync(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            if (item.Token.State != ProgressToken.ProgressState.Installed) return;
            if (!Directory.Exists($"{installDir}/{item.Id}")) return;
            await Task.Run(() => Directory.Delete($"{installDir}/{item.Id}", true));
        }
        #endregion
    }

    public class AcquireItemJob<T> : IDisposable
        where T : IRepositoryItem
    {
        #region Properties
        /// <summary>
        /// The item to be downloaded.
        /// </summary>
        public T MainItem { get; }
        /// <summary>
        /// Items to be downloaded and installed, including MainItem and all its dependencies.
        /// </summary>
        public List<T> ItemsToAcquire { get; } = new();
        #endregion

        #region Constructors
        public AcquireItemJob(T item)
        {
            MainItem = item ?? throw new ArgumentNullException(nameof(item));

            // Get list of dependencies
            if (item.Dependencies != null)
            {
                List<T> dependencies = new();
                GetDependencies(item, ref dependencies);

                foreach (T dependency in dependencies)
                {
                    if (dependency.Token.State == ProgressToken.ProgressState.Installed) continue;
                    ItemsToAcquire.Add(dependency);
                }
            }
            else
            {
                ItemsToAcquire.Add(item);
            }
        }
        #endregion

        #region Methods
        protected void GetDependencies(T item, ref List<T> dependencies)
        {
            if (!dependencies.Contains(item))
            {
                dependencies.Add(item);

                if (item.Dependencies != null)
                {
                    foreach (var kvp in item.Dependencies)
                    {
                        GetDependencies((T)kvp.Value, ref dependencies);
                    }
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
