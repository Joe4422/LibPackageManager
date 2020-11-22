using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;

namespace LibPackageManager.Repositories
{
    /// <summary>
    /// An item provided by a repository.
    /// </summary>
    public interface IRepositoryItem
    {
        #region Properties
        /// <summary>
        /// The item's unique identifier.
        /// </summary>
        string Id { get; }
        /// <summary>
        /// Token to represent the item's download/install progress.
        /// </summary>
        ProgressToken Token { get; }
        /// <summary>
        /// The URL from which the item can be downloaded.
        /// </summary>
        string DownloadUrl { get; }
        /// <summary>
        /// List of items that this item relies on.
        /// </summary>
        Dictionary<string, IRepositoryItem> Dependencies { get; }
        #endregion
    }

    public class ProgressToken : INotifyPropertyChanged
    {
        #region Enums
        public enum ProgressState
        {
            NotStarted,
            DownloadInProgress,
            Downloaded,
            InstallInProgress,
            Installed,
            Failed
        }
        #endregion

        #region Properties
        /// <summary>
        /// The download progress percentage, from 0% to 100%.
        /// </summary>
        private int downloadPercentage; public int DownloadPercentage
        {
            get => downloadPercentage;
            protected set
            {
                downloadPercentage = value;
                PropertyChanged?.Invoke(this, new(nameof(DownloadPercentage)));
            }
        }

        /// <summary>
        /// The state of the item's download.
        /// </summary>
        private ProgressState state = ProgressState.NotStarted; public ProgressState State
        {
            get => state;
            set
            {
                state = value;
                PropertyChanged?.Invoke(this, new(nameof(State)));
            }
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void SetDownloadClient(WebClient client)
        {
            client.DownloadProgressChanged += DownloadProgressChangedHandler;
        }

        /// <summary>
        /// Updates the progress percentage.
        /// </summary>
        protected void DownloadProgressChangedHandler(object sender, DownloadProgressChangedEventArgs e)
        {
            if (State == ProgressState.DownloadInProgress)
            {
                DownloadPercentage = e.ProgressPercentage;
            }
        }
        #endregion
    }
}
