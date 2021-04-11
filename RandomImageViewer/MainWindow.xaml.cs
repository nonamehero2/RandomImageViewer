using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Threading;
using System.Security.Cryptography;
using Path = System.IO.Path;

namespace RandomImageViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string SEARCH_OPTIONS = "*.jpg,*.jpeg,*.png,*.gif,*.tiff,*.bmp,*.svg";
        const string TRASH_DIR = @".\trash\";
        private string CurrDirectory = Environment.CurrentDirectory + @"\images";
        private int ImageIndex = 0;
        const double GridOpacity = 1;
        const double GridOpacityInactive = .3;
        private bool IsRunning = true;
        private List<string> Files = new List<string>();
        private List<int> PreviousIndexes = new List<int>();
        private Mutex Mutex = new Mutex();
        private Thread MainThread;

        public MainWindow()
        {
            InitializeComponent();

            ToggleButtons( false );

            try
            {
                ClearAndPopulateFiles();
                if ( Files.Count() > 0 )
                {
                    ButtonGrid.Opacity = GridOpacityInactive;
                    ToggleButtons( true );
                    IndexRandomly( false );
                    DisplayImage();
                }
            }
            catch ( Exception )
            {

            }

            MainThread = new Thread( new ThreadStart( ThreadMethod ) );
            MainThread.Start();
        }

        void ThreadMethod()
        {
            try
            {
                while( IsRunning )
                {
                    lock ( Mutex )
                    {
                        ClearAndPopulateFiles();
                    }
                    Thread.Sleep( 5000 );
                }

            }
            catch ( Exception )
            {
                // DO NOTHING
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            IsRunning = false;
            MainThread.Join();
        }

        private void ButtonPrevious_Click( object sender, RoutedEventArgs e )
        {
            lock (Mutex)
            {
                if (PreviousIndexes.Count > 0)
                {
                    ImageIndex = PreviousIndexes.Last();
                    PreviousIndexes.RemoveAt(PreviousIndexes.Count - 1);
                }
            }

            ButtonPrevious.Content = "Previous(" + PreviousIndexes.Count + ")";

            if (PreviousIndexes.Count == 0)
            {
                ButtonPrevious.Content = "Previous";
            }

            DisplayImage();
        }

        private void ButtonLeft_Click( object sender, RoutedEventArgs e)
        {
            lock (Mutex)
            {
                AddItemToPreviousItems();
                --ImageIndex;
            }

            DisplayImage( );
        }

        private void ButtonRight_Click(object sender, RoutedEventArgs e)
        {
            lock (Mutex)
            {
                AddItemToPreviousItems();
                ++ImageIndex;
            }

            DisplayImage( );
        }

        private void Window_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                ButtonLeft_Click(sender, null);
            }
            else if (e.Delta < 0)
            {
                ButtonRight_Click(sender, null);
            }
        }

        private void ButtonOpen_Click( object sender, RoutedEventArgs e )
        {
            try
            {
                using ( var fbd = new System.Windows.Forms.FolderBrowserDialog() )
                {
                    System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                    if ( result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace( fbd.SelectedPath ) )
                    {
                        lock ( Mutex )
                        {
                            CurrDirectory = fbd.SelectedPath;
                            ClearAndPopulateFiles();
                        }

                        ClearPreviousItems();
                        DisplayImage();
                        ToggleButtons( true );
                    }
                }
            }
            catch( Exception )
            {

            }

        }

        private void ButtonRandom_Click( object sender, RoutedEventArgs e )
        {
            IndexRandomly( true );

            DisplayImage();
        }


        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(TRASH_DIR))
            {
                Directory.CreateDirectory(TRASH_DIR);
            }

            int indexCache = ImageIndex;

            lock (Mutex)
            {
                string currentFile = Files[indexCache];
                File.Move(currentFile, TRASH_DIR + Path.GetFileName(currentFile));
                ClearAndPopulateFiles();
            }

            DisplayImage();

            ToggleButtons(true);
        }
        
        private void ButtonGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ButtonGrid.Opacity = GridOpacity;
        }

        private void ButtonGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ButtonGrid.Opacity = GridOpacityInactive;
        }

        private void IndexRandomly( bool addToPrevious )
        {
            int randomValue = 0;
            using ( RNGCryptoServiceProvider rg = new RNGCryptoServiceProvider() )
            {
                byte[] rno = new byte[5];
                rg.GetBytes( rno );
                randomValue = BitConverter.ToInt32( rno, 0 );
            }

            lock ( Mutex )
            {
                if( addToPrevious )
                {
                    AddItemToPreviousItems();
                }
                ImageIndex = randomValue;
            }
        }

        private void AddItemToPreviousItems()
        {
            PreviousIndexes.Add( 0 + ImageIndex );

            if ( PreviousIndexes.Count > 10 )
            {
                PreviousIndexes.RemoveAt( 0 );
            }

            ButtonPrevious.Content = "Previous(" + PreviousIndexes.Count + ")";
        }

        private void ClearPreviousItems()
        {
            PreviousIndexes.Clear();
            ButtonPrevious.Content = "Previous";
        }

        private void ClearAndPopulateFiles()
        {
            Files.Clear();

            foreach (string imageFile in Directory.GetFiles(CurrDirectory, "*.*", SearchOption.TopDirectoryOnly).Where(s => SEARCH_OPTIONS.Contains(System.IO.Path.GetExtension(s).ToLower())))
            {
                Files.Add(imageFile);
            }
        }

        private void ToggleButtons(bool state)
        {
            ButtonLeft.IsEnabled = state;
            ButtonPrevious.IsEnabled = state;
            ButtonDelete.IsEnabled = state;
            ButtonRandom.IsEnabled = state;
            ButtonRight.IsEnabled = state;
        }

        void DisplayImage()
        {
            bool tryAgain = false;

            lock (Mutex)
            {
                if (ImageIndex < 0)
                {
                    ImageIndex = Files.Count() + ImageIndex;
                }

                ImageIndex %= Files.Count();
                ImageIndex = Math.Abs(ImageIndex);

                if (Files.Count() > 0)
                {
                    Title = Path.GetFileName(Files[ImageIndex]);
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(Files[ImageIndex]);
                    image.EndInit();
                    imageLeft.Source = image;

                }

                if (tryAgain)
                {
                    lock (Mutex)
                    {
                        ClearAndPopulateFiles();
                    }
                    DisplayImage();
                }
            }
        }
    }
}
