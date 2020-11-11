using LibPackageManager.Repositories;
using System;
using System.Collections.Generic;
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
        where T : class, IRepositoryItem
    {
        #region Variables
        protected string downloadDir;
        protected string installDir;
        #endregion

        #region Events
        public delegate void DownloadStartedEventHandler(object sender, DownloadToken<T> token);
        /// <summary>
        /// Indicates that a download has been initiated.
        /// </summary>
        public event DownloadStartedEventHandler DownloadStarted;
        #endregion

        #region Properties
        /// <summary>
        /// Contains all items that are currently being downloaded.
        /// </summary>
        public List<DownloadToken<T>> DownloadsInProgress { get; } = new();
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
        /// <returns>True if the operation succeeded, false otherwise.</returns>
        public async Task<bool> GetItemAsync(T item)
        {
            // Check item is not null
            if (item is null) throw new ArgumentNullException(nameof(item));

            // Create download task list and start downloading item
            List<Task<bool>> itemDownloadTasks = new()
            {
                GetSingleItemAsync(item)
            };

            // Check if this item has dependencies
            if (item is IDependentRepositoryItem depItem)
            {
                // Start each dependency's download and add it to the download list
                foreach (string key in depItem.Dependencies.Keys)
                {
                    itemDownloadTasks.Add(GetItemAsync(depItem.Dependencies[key] as T));
                }
            }

            // Wait until all tasks are completed
            await Task.WhenAll(itemDownloadTasks);

            return !itemDownloadTasks.Select(x => x.Result).Contains(false);
        }

        /// <summary>
        /// Downloads and installs an item.
        /// </summary>
        /// <param name="item">The item to download and install.</param>
        /// <exception cref="ArgumentNullException"
        protected async Task<bool> GetSingleItemAsync(T item)
        {
            // Check item is not null
            if (item is null) throw new ArgumentNullException(nameof(item));

            // Check for conditions that could cause the download to be unnecessary
            if (item.IsDownloaded) return true;
            else if (item.DownloadUrl == null) return false;
            else if (DownloadsInProgress.Select(x => x.ItemToDownload).Contains(item)) return true;

            using WebClient client = new();

            // Set up the download token
            DownloadToken<T> token = new DownloadToken<T>(item, client);
            DownloadsInProgress.Add(token);
            DownloadStarted.Invoke(this, token);

            string downloadPath = $"{downloadDir}/{Path.GetFileName(item.DownloadUrl)}";

            // Ensure directories exist by creating them
            try
            {
                Directory.CreateDirectory(downloadDir);
                Directory.CreateDirectory(installDir);
            }
            catch (Exception)
            {
                return false;
            }

            // Download file
            try
            {
                await client.DownloadFileTaskAsync(item.DownloadUrl, downloadPath);
            }
            catch (Exception)
            {
                return false;
            }

            // Install item from file
            await InstallItemAsync(item, downloadPath);

            // Mark item as downloaded
            item.InstallPath = $"{installDir}/{item.Id}";

            DownloadsInProgress.Remove(token);

            // Remove downloaded file now we've installed its content
            File.Delete(downloadPath);

            return true;
        }

        /// <summary>
        /// Uninstalls an item.
        /// </summary>
        /// <param name="item">The item to uninstall.</param>
        public async Task RemoveItemAsync(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            if (!item.IsDownloaded) return;
            if (!Directory.Exists($"{installDir}/{item.Id}")) return;
            await Task.Run(() => Directory.Delete($"{installDir}/{item.Id}", true));
            item.InstallPath = null;
        }

        /// <summary>
        /// Installs an item to the specified directory.
        /// </summary>
        /// <param name="item">The item to install.</param>
        /// <param name="downloadedFilePath">Path to the item's downloaded content.</param>
        protected abstract Task InstallItemAsync(T item, string downloadedFilePath);
        #endregion
    }

    public class DownloadToken<T>
        where T : class, IRepositoryItem
    {
        #region Properties
        /// <summary>
        /// The item currently being downloaded.
        /// </summary>
        public T ItemToDownload { get; }

        /// <summary>
        /// The download progress percentage, from 0% to 100%.
        /// </summary>
        public int ProgressPercentage { get; protected set; }

        /// <summary>
        /// Whether the download has finished.
        /// </summary>
        public bool IsCompleted => ProgressPercentage == 100;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new DownloadToken.
        /// </summary>
        /// <param name="item">The item that is being downloaded.</param>
        /// <param name="downloadClient">The WebClient used to download the item.</param>
        public DownloadToken(T item, WebClient downloadClient)
        {
            ItemToDownload = item ?? throw new ArgumentNullException(nameof(item));
            if (downloadClient is null) throw new ArgumentNullException(nameof(item));

            downloadClient.DownloadProgressChanged += DownloadProgressChangedHandler;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Updates the progress percentage.
        /// </summary>
        private void DownloadProgressChangedHandler(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressPercentage = e.ProgressPercentage;
        }
        #endregion
    }
}
