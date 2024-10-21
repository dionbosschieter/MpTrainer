using System.Windows;
using System.Windows.Controls;
using Memory;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using static MpTrainer.Keyboard;

namespace MpTrainer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // game constants
        private const string appName = "mr.dll";
        private const string locationMeditation = "base+0x7F3DD4";
        private const string locationMagicGuardian = "base+0x7F3F18";
        private const string hpLocation = "base+0x007EC208,0xBB8,0xF8,0x34,0x24,0x1C,0x50,0x190";
        private const string mpLocation = "base+0x007EC208,0xAF8,0x24,0xBBC,0x40,0x7C,0x190";
        private const string xLocation = "base+0x007ED788,0x648,0x24,0x5C,0x4,0x24,0x5D4";

        // game state vars
        private int hp = 0;
        private int mp = 0;
        private bool buff1Enabled = false;
        private bool buff2Enabled = false;
        private bool connected = false;
        private int charLocationX = 0;

        // trainer setting vars
        private bool autoBuff;
        private bool autoPot;
        private bool autoAttack;
        private bool autoChangeChannel;
        private int minMp = 0;
        private int minHp = 0;
        private int changeChannelCounterThreshold = 1000;
        private bool stopBackgoundThreads = false;
        private int changeChannelCounter = 0;

        // movement detection vars
        private bool movingLeft = true;
        private int minX;
        private int maxX;

        private Mem m = new Mem();
        private Process p;
        private Thread backgroundThread;

        [DllImport("User32.dll")] static extern int SetFocus(IntPtr point);
        [DllImport("User32.dll")] static extern int SetForegroundWindow(IntPtr point); 

        public MainWindow()
        {
            InitializeComponent();

            minHp = Int32.Parse(HpTextBox.Text);
            minMp = Int32.Parse(MpTextBox.Text);
            autoBuff = AutoBuffCheckbox.IsChecked == true;
            autoPot = AutoPotCheckbox.IsChecked == true;
            autoAttack = AutoAttackCheckbox.IsChecked == true;
            autoChangeChannel = AutoChangeChannelCheckbox.IsChecked == true;
            Connect();
        }

        private async Task TrainerThread()
        {
            while (stopBackgoundThreads == false)
            {
                RefreshFromMemoryVariables();
                Dispatcher.BeginInvoke(new Action(() => { RefreshForms(); }));
                
                if (autoBuff) PerformBuffActions();
                if (autoPot) PerformHealthActions();

                if (autoAttack)
                {
                    PerformMove();
                    PerformAttack();
                }

                if (needToChangeChannel())
                {
                    ChangeChannel();
                }

                Thread.Sleep(250);
            }
        }

        private bool needToChangeChannel()
        {
            if (autoChangeChannel && ++changeChannelCounter > changeChannelCounterThreshold)
            {
                changeChannelCounter = 0;
                return true;
            }

            return false;
        }

        private void PerformMove()
        {
            // decide direction on charLocationX and where we are moving
            if (movingLeft)
            {
                movingLeft = charLocationX != minX;
            }
            if (movingLeft == false)
            {
                movingLeft = charLocationX == maxX;
            }

            minX = charLocationX < minX ? charLocationX : minX;
            maxX = charLocationX > maxX ? charLocationX : maxX;

            Keyboard.KeyStroke key = movingLeft ? 
                Keyboard.KeyStroke.LEFTARROW : 
                Keyboard.KeyStroke.RIGHTARROW;

            for (int i = 0; i < 3; i++)
            {
                SendKeyStroke(key, 50, false);

                for (int j = 0; j < 5; j++)
                    SendKeyStroke(Keyboard.KeyStroke.Z, 25);
            }

            SendKeyStroke(key, 50);
        }

        private void ChangeChannel()
        {
            Thread.Sleep(7500); // sleep for rest
            SendKeyStroke(KeyStroke.ESCAPE);
            SendKeyStroke(KeyStroke.RETURN);
            SendKeyStroke(KeyStroke.RIGHTARROW);
            SendKeyStroke(KeyStroke.RETURN);
        }

        private void PerformAttack()
        {
            SendKeyStroke(Keyboard.KeyStroke.T);
        }


        private void PerformBuffActions()
        {
            if (!buff1Enabled)
                SendKeyStroke(Keyboard.KeyStroke.END, 200);
            if (!buff2Enabled)
                SendKeyStroke(Keyboard.KeyStroke.DELETE, 200);
        }
        private void PerformHealthActions()
        {
            if (hp < minHp)
                SendKeyStroke(Keyboard.KeyStroke.PGUP);
            if (mp < minMp)
                SendKeyStroke(Keyboard.KeyStroke.PGDN);
        }

        private void SendKeyStroke(Keyboard.KeyStroke key, int delay = 100, bool simulateUp = true)
        {
            IntPtr h = p.MainWindowHandle;
            SetFocus(h);
            SetForegroundWindow(h);
            
            Thread.Sleep(delay);
            Keyboard.SendKey(key, false, Keyboard.InputType.Keyboard);

            if (simulateUp)
            {
                Thread.Sleep(delay);
                Keyboard.SendKey(key, true, Keyboard.InputType.Keyboard);
            }
        }

        private bool getSkillActive(string skillLocation)
        {
            bool[] bits = m.ReadBits(skillLocation);
            if (bits.Length > 0)
            {
                return bits[0];
            }

            return false;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (HpTextBox != null)
            {
                minHp = Int32.Parse(HpTextBox.Text);
            }
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            if (MpTextBox != null)
            {
                minMp = Int32.Parse(MpTextBox.Text);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void Connect()
        {
            p = Process.GetProcessesByName(appName).FirstOrDefault();
            connected = m.OpenProcess(appName);

            if (connected)
            {
                ConnectButton.Content = "Connected";
                RefreshFromMemoryVariables();
                RefreshForms();
            }
        }

        private void RefreshFromMemoryVariables()
        {
            if (!connected)
                return;

            buff1Enabled = getSkillActive(locationMeditation);
            buff2Enabled = getSkillActive(locationMagicGuardian);

            hp = m.Read2Byte(hpLocation);
            mp = m.Read2Byte(mpLocation);
            charLocationX = m.ReadInt(xLocation);
        }

        private void RefreshForms()
        {
            Buff1Label.Content = buff1Enabled.ToString();
            Buff2Label.Content = buff2Enabled.ToString();

            HpLabel.Content = String.Format("{0}", hp);
            MpLabel.Content = String.Format("{0}", mp);
            XLabel.Content = String.Format("{0}", charLocationX);
            MinXLabel.Content = String.Format("{0}", minX);
            MaxXLabel.Content = String.Format("{0}", maxX);
            MaxXLabel.Content = String.Format("{0}", maxX);
            ChannelCounterLabel.Content = String.Format("{0}", changeChannelCounter);
        }

        private void StartPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!connected)
            {
                return;
            }

            if (backgroundThread != null && backgroundThread.IsAlive)
            {
                stopBackgoundThreads = true;
                
                StartPauseButton.Background = Brushes.LightGray;
                StartPauseButton.Content = "Resume";
            }
            else if (backgroundThread == null || backgroundThread.IsAlive == false)
            {
                stopBackgoundThreads = false;
                backgroundThread = new Thread(async () => await TrainerThread());
                backgroundThread.Start();

                StartPauseButton.Background = Brushes.LightGreen;
                StartPauseButton.Content = "Pause";
                maxX = -9999;
                minX = 9999;
            }
        }

        private void AutoAttackCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            autoAttack = !autoAttack;
        }

        private void AutoPotCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            autoPot = !autoPot;
        }

        private void AutoBuffCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            autoBuff = !autoBuff;
        }

        private void AutoChangeChannelCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            autoChangeChannel = !autoChangeChannel;
        }
    }
}