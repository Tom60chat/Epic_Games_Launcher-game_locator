using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace EpicLauncher_GameLocator
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string LaunchExecutable;
        private string InstallLocation;
        private const string APP_ID = "com.KatyCorp.EpicLocator";

        public MainWindow()
        {
            InitializeComponent();

            _ = KatyLauncher.API.Tools.KatyLauncherAPIHelper.Check(APP_ID);

            Reset();
        }

        private void Reset()
        {
            Locate_Button.Visibility = Visibility.Visible;
            Activity.Visibility = Visibility.Collapsed;
        }

        private void Locate_Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LaunchExecutable))
            {
                Error(@"First select the game with the Browse button");
                return;
            }
            else
            {
                Locate();
            }
        }

        private void GameBrowse_Button_Click(object sender, RoutedEventArgs e)
        {
            // Locate the .exe
            var openFileDialog = new OpenFileDialog();
            if (Environment.Is64BitOperatingSystem)
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Epic Games";
            else
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Epic Games";

            openFileDialog.Title = "Select the main executable of the game";
            openFileDialog.Filter = "game (*.exe)|*.exe";

            if (openFileDialog.ShowDialog() == true)
            {
                //Get the path of specified file
                LaunchExecutable = Path.GetFileName(openFileDialog.FileName);
                var installLocation = Path.GetDirectoryName(openFileDialog.FileName);
                GamePath.Text = openFileDialog.FileName;
                if (Directory.Exists(Path.Combine(installLocation, ".egstore")))
                {
                    InstallLocation = installLocation;
                    EgStorePath.Text = openFileDialog.FileName;
                }
            }
        }

        private void EgStoreBrowse_Button_Click(object sender, RoutedEventArgs e)
        {
            // Locate the .egstore

            var openFileDialog = new CommonOpenFileDialog();

            if (Environment.Is64BitOperatingSystem)
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Epic Games";
            else
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Epic Games";

            openFileDialog.Title = "Select the game folder";
            openFileDialog.IsFolderPicker = true;

            if (openFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                //Get the path of specified file
                InstallLocation = openFileDialog.FileName;
                EgStorePath.Text = openFileDialog.FileName;
            }
        }

        private void Locate()
        {
            Locate_Button.Visibility = Visibility.Collapsed;
            Activity.Visibility = Visibility.Visible;
            Activity.Text = @"Search for the "".egstore"" folder";

            string ManifestLocation = InstallLocation + "/.egstore";

            // Find the ".egstore" folder
            if (!Directory.Exists(ManifestLocation))
            {
                Error(@"This file does not contain a "".egstore"" folder.");
                return;
            }

            // Read ".mancpn" file
            Activity.Text = @"Checking the "".manifest"" file";

            if (Directory.GetFiles(ManifestLocation, "*.manifest").Length == 0)
            {
                var manifests = Directory.GetFiles(Path.Combine(ManifestLocation, "Pending"), "*.manifest");
                if (manifests.Length != 0)
                {
                    foreach (var manifest in manifests)
                        File.Move(manifest, Path.Combine(ManifestLocation, Path.GetFileName(manifest)));
                }
                else
                {
                    Error(@"The "".egstore"" folder does not contain the ""* .manifest"" file.");
                    return;
                }
            }

            // Read ".mancpn" file
            Activity.Text = @"Reading the "".mancpn"" file";

            var mancpns = Directory.GetFiles(ManifestLocation, "*.mancpn");

            if (mancpns.Length == 0)
            {
                mancpns = Directory.GetFiles(Path.Combine(ManifestLocation, "Pending"), "*.mancpn");
                if (mancpns.Length != 0)
                {
                    foreach (var _mancpn in mancpns)
                        File.Move(_mancpn, Path.Combine(ManifestLocation, Path.GetFileName(_mancpn)));
                    mancpns = Directory.GetFiles(ManifestLocation, "*.mancpn");
                }
                else
                {
                    Error(@"The "".egstore"" folder does not contain the ""* .mancpn"" file.");
                    return;
                }
            }

            var mancpn = mancpns.First();
            string InstallationGuid = Path.GetFileNameWithoutExtension(mancpn);

            JsonTextReader reader = new JsonTextReader(new StreamReader(mancpn));

            string Value = "";
            string AppName = "";
            string CatalogItemId = "";
            string CatalogNamespace = "";

            try
            {
                while (reader.Read())
                {
                    if (reader.Value != null)
                    {
                        switch (Value)
                        {
                            case "AppName":
                                AppName = reader.Value.ToString();
                                break;

                            case "CatalogItemId":
                                CatalogItemId = reader.Value.ToString();
                                break;

                            case "CatalogNamespace":
                                CatalogNamespace = reader.Value.ToString();
                                break;
                        }

                        Value = reader.Value.ToString();
                    }
                }
            }
            catch (JsonReaderException JRe)
            {
                reader.Close();
                Error("JsonReaderException: file possibly corrupted or unreadable." + Environment.NewLine + JRe.Message);
                return;
            }

            reader.Close();

            if (string.IsNullOrEmpty(AppName) || string.IsNullOrEmpty(CatalogItemId) || string.IsNullOrEmpty(CatalogNamespace))
            {
                Error("Incomplete information: file possibly corrupted or unreadable.");
                return;
            }

            // Addition of the game in "LauncherInstalled.dat"
            Activity.Text = $@"Addition of {AppName} in ""LauncherInstalled.dat""";

            string LauncherInstalledLocation = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Epic\UnrealEngineLauncher\LauncherInstalled.dat";
            Installation installation = new Installation
            {
                InstallLocation = InstallLocation,
                AppName = AppName,
                AppVersion = ""
            };

            string Text = File.ReadAllText(LauncherInstalledLocation);

            Text = Text.Insert(Text.LastIndexOf(']') - 3, "," + JsonConvert.SerializeObject(installation));

            File.WriteAllText(LauncherInstalledLocation, Text);

            reader.Close();

            // Addition of the game in Manifests fodler
            Activity.Text = $@"Addition of {AppName} in Manifests fodler";

            string ManifestsLocation = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Epic\EpicGamesLauncher\Data\Manifests";
            Item item = new Item()
            {
                LaunchExecutable = LaunchExecutable,
                ManifestLocation = ManifestLocation,
                AppName = AppName,
                CatalogItemId = CatalogItemId,
                CatalogNamespace = CatalogNamespace,
                FullAppName = AppName + " : Live",
                InstallationGuid = InstallationGuid,
                InstallLocation = InstallLocation,
                StagingLocation = InstallLocation + "/.egstore/bps",
                InstallSize = (int)DirSize(new DirectoryInfo(InstallLocation)),
                MainGameAppName = AppName,
                MandatoryAppFolderName = Path.GetDirectoryName(InstallLocation)
            };

            if (!File.Exists(ManifestsLocation)) Directory.CreateDirectory(ManifestsLocation);

            File.WriteAllText($@"{ManifestsLocation}\{InstallationGuid}.item", JsonConvert.SerializeObject(item));

            // Done !
            Activity.Text = $@"Done !";

            MessageBox.Show(
                @"Done !" + Environment.NewLine +
                @"Your game should appear as ""Launch"" or ""Repair""" + Environment.NewLine +
                @"If something is wrong Verify Integrity of Game Files with EpicGamesLauncher");

            Reset();
        }

        private void Error(string Cause)
        {
            MessageBox.Show(Cause + Environment.NewLine /*+ "For more information read the readme file."*/);
            Reset();
        }

        public static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size;
        }
    }

    internal struct Installation
    {
        public string InstallLocation;
        public string AppName;
        public string AppVersion;
    }

    internal struct Item
    {
        public int FormatVersion;
        public bool bIsIncompleteInstall;
        public string AppVersionString;
        public string LaunchCommand;
        public string LaunchExecutable;
        public string ManifestLocation;
        public bool bIsApplication;
        public bool bIsExecutable;
        public bool bIsManaged;
        public bool bNeedsValidation;
        public bool bRequiresAuth;
        public bool bCanRunOffline;
        public string AppName;
        public string[] BaseURLs;
        public string BuildLabel;
        public string CatalogItemId;
        public string CatalogNamespace;
        public string[] AppCategories;
        public string[] ChunkDbs;
        public string[] CompatibleApps;
        public string DisplayName;
        public string FullAppName;
        public string InstallationGuid;
        public string InstallLocation;
        public string InstallSessionId;
        public string[] InstallTags;
        public string[] InstallComponents;
        public string HostInstallationGuid;
        public string[] PrereqIds;
        public string StagingLocation;
        public string TechnicalType;
        public string VaultThumbnailUrl;
        public string VaultTitleText;
        public int InstallSize;
        public string MainWindowProcessName;
        public string[] ProcessNames;
        public string MainGameAppName;
        public string MandatoryAppFolderName;
        public string OwnershipToken;

        public Item(int nothing = 0) : this()
        {
            AppVersionString = "";
            LaunchCommand = "";
            bIsApplication = true;
            bIsExecutable = true;
            bRequiresAuth = true;
            bCanRunOffline = true;
            BaseURLs = new string[0];
            BuildLabel = "Live";
            AppCategories = new string[3] { "public", "games", "applications" };
            ChunkDbs = new string[0];
            CompatibleApps = new string[0];
            DisplayName = "";
            InstallSessionId = "";
            InstallTags = new string[0];
            InstallComponents = new string[0];
            HostInstallationGuid = "00000000000000000000000000000000";
            PrereqIds = new string[0];
            TechnicalType = "public,games,applications";
            VaultThumbnailUrl = "";
            VaultTitleText = "";
            MainWindowProcessName = "";
            ProcessNames = new string[0];
            OwnershipToken = "false";
        }
    }
}
