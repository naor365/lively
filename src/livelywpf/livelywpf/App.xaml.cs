﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Interop;
using System.Globalization;

using Props = livelywpf.Properties;
using MahApps.Metro;
using livelywpf.Lively.Helpers;

namespace livelywpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        static Mutex mutex = new Mutex(false, "LIVELY:DESKTOPWALLPAPERSYSTEM");
        public static MainWindow w = null;

        //folder paths
        public static readonly string pathData = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lively Wallpaper");
        /*
        public static string pathSaveData = Path.Combine(pathData, "SaveData");
        public static string pathWpTmp = Path.Combine(pathData, "SaveData", "wptmp");
        public static string pathWallpapers = Path.Combine(pathData, "wallpapers");
        public static string pathTmpData = Path.Combine(pathData, "tmpdata");
        public static string pathWpData = Path.Combine(pathData, "tmpdata", "wpdata");
        */
        protected override void OnStartup(StartupEventArgs e)
        {
            //delete residue tempfiles if any!
            FileOperations.EmptyDirectory(Path.Combine(pathData, "tmpdata"));
            try
            {
                //create directories if not exist
                Directory.CreateDirectory(Path.Combine(pathData, "SaveData"));
                Directory.CreateDirectory(Path.Combine(pathData, "SaveData", "wptmp"));
                Directory.CreateDirectory(Path.Combine(pathData, "wallpapers"));
                Directory.CreateDirectory(Path.Combine(pathData, "tmpdata"));
                Directory.CreateDirectory(Path.Combine(pathData, "tmpdata", "wpdata"));
            }
            catch(Exception ex)
            {
                //not logging here, just display & terminate.
                MessageBox.Show(ex.Message, Props.Resources.txtLivelyErrorMsgTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            SaveData.LoadConfig();

            #region language
            //CultureInfo.CurrentCulture = new CultureInfo("ru-RU", false); //not working?
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(SaveData.config.Language);
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("zh-CN"); //zh-CN
            #endregion language

            if (!SaveData.config.SafeShutdown)
            {
                //clearing previous wp persisting image if any (not required, subProcess clears it).
                SetupDesktop.RefreshDesktop();

                Directory.CreateDirectory( Path.Combine(pathData, "ErrorLogs"));
                string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt";
                if (File.Exists( Path.Combine(pathData, "ErrorLogs", fileName) ))
                    fileName = Path.GetRandomFileName() + ".txt";

                try
                {                    
                    File.Copy( Path.Combine(pathData, "logfile.txt"),
                            Path.Combine(pathData, "ErrorLogs", fileName));
                }
                catch(IOException e1)
                {
                    System.Diagnostics.Debug.WriteLine(e1.ToString());    
                }
                
                var result = MessageBox.Show(Props.Resources.msgSafeModeWarning +
                    Path.Combine(pathData, "ErrorLogs", fileName)
                    , Props.Resources.txtLivelyErrorMsgTitle, MessageBoxButton.YesNo);

                if (result == MessageBoxResult.No)
                {
                    SetupDesktop.wallpapers.Clear();
                    SaveData.SaveWallpaperLayout(); //deleting saved wallpaper arrangements.
                }

            }
            SaveData.config.SafeShutdown = false;
            SaveData.SaveConfig();

            #region theme
            // add custom accent and theme resource dictionaries to the ThemeManager
            // you should replace MahAppsMetroThemesSample with your application name
            // and correct place where your custom accent lives
            //ThemeManager.AddAccent("CustomAccent1", new Uri("pack://application:,,,/CustomAccent1.xaml"));

            // get the current app style (theme and accent) from the application
            // you can then use the current theme and custom accent instead set a new theme
            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);

            
            // setting accent & theme
            ThemeManager.ChangeAppStyle(Application.Current,
                                        ThemeManager.GetAccent(SaveData.livelyThemes[SaveData.config.Theme].Accent),
                                        ThemeManager.GetAppTheme(SaveData.livelyThemes[SaveData.config.Theme].Base)); // or appStyle.Item1
                                        
            // now change app style to the custom accent and current theme
            //ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent("CustomAccent1"), ThemeManager.GetAppTheme(SaveData.livelyThemes[SaveData.config.Theme].Base));
            #endregion theme

            base.OnStartup(e);

            SetupExceptionHandling();
            w = new MainWindow(); 
        
            if (SaveData.config.IsFirstRun)
            {
                //SaveData.config.isFirstRun = false; //only after minimizing to tray isFirstRun is set to false.
                SaveData.SaveConfig(); //creating disk file temp, not needed!

                w.Show();
                w.UpdateWallpaperLibrary(); 

                Dialogues.HelpWindow hw = new Dialogues.HelpWindow
                {
                    Owner = w,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                hw.ShowDialog();              
            }

            if(SaveData.config.IsRestart)
            {
                SaveData.config.IsRestart = false;
                SaveData.SaveConfig();

                w.Show();
                w.UpdateWallpaperLibrary();
                //w.ShowMainWindow();

                w.tabControl1.SelectedIndex = 2; //settings tab
                //SetupDesktop.SetFocus();
                //w.Activate();
            }
            
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0} v{1}", assemblyName.Name, assemblyName.Version);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception in LogUnhandledException");
            }
            finally
            {
                LogSavedData();
                Logger.Error(message + "\n" + exception.ToString());                
            }

            //making the external process livelymonitor.exe close running wp's instead.
            //SetupDesktop.CloseAllWallpapers();
        }

        private bool _savedDataLogged = false;

        private void LogSavedData()
        {
            if (!_savedDataLogged)
            {
                Logger.Info("Saved config file:-\n" + SaveData.PropertyList(SaveData.config));
                _savedDataLogged = true;
            }
        }

        void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            /*
            // Ask the user if they want to allow the session to end
            //string msg = string.Format("{0}. End session?", e.ReasonSessionEnding);
            //MessageBoxResult result = MessageBox.Show(msg, "Session Ending", MessageBoxButton.YesNo);

            // End session, if specified
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            */

            if(e.ReasonSessionEnding == ReasonSessionEnding.Shutdown || e.ReasonSessionEnding == ReasonSessionEnding.Logoff)
            {
                e.Cancel = true; //delay shutdown till lively close properly.
                if (w != null)
                    w.ExitApplication();
            }
            
        }

        [STAThread]
        public static void Main()
        {
            //NotifyIcon Fix: https://stackoverflow.com/questions/28833702/wpf-notifyicon-crash-on-first-run-the-root-visual-of-a-visualtarget-cannot-hav/29116917
            //Rarely I get this error "The root Visual of a VisualTarget cannot have a parent..", hard to pinpoint not knowing how to recreate the error.
            System.Windows.Controls.ToolTip tt = new System.Windows.Controls.ToolTip();
            tt.IsOpen = true;
            tt.IsOpen = false;

            try
            {
                //if (!mutex.WaitOne()) //indefinite wait.
                // wait a few seconds in case livelywpf instance is just shutting down..
                if (!mutex.WaitOne(TimeSpan.FromSeconds(5), false))
                {
                    //this is ignoring the config-file saved language, only checking system language.
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(CultureInfo.CurrentCulture.Name); 
                    MessageBox.Show(Props.Resources.msgSingleInstanceOnly, Props.Resources.txtLivelyWaitMsgTitle, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }
            catch(AbandonedMutexException e)
            {
                //Note to self:- logger backup(in the even of previous lively crash) is at App() contructor func, DO NOT start writing loghere to avoid overwriting crashlog.
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            try
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();
            }
            finally { mutex.ReleaseMutex(); }
        }

    }
}
