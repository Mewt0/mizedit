using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.VisualBasic;
using MizEdit.Core;
using MizEdit.Services;
using NAudio.Wave;
using NAudio.Vorbis;

namespace MizEdit
{
    public partial class MainWindow : Window
    {
        private readonly MissionService _missionService = new();
        private readonly BatchService _batchService;
        private MissionSession? _session;
        private WaveOutEvent? _waveOut;
        private VorbisWaveReader? _vorbisReader;
        private string? _selectedAudioFile;
        private MissionLua.RadioMessage? _selectedRadioMessage;
        private List<MissionLua.RadioMessage> _radioTransmissions = new();

        // Radio lists by source
        private List<MissionLua.RadioMessage> _radioFromDictionary = new();
        private List<MissionLua.RadioMessage> _radioFromTasks = new();

        private enum RadioSourceMode { Dictionary = 0, Tasks = 1, Both = 2 }
n        public MainWindow()
        {
            InitializeComponent();
            SetEnabled(false);
            _batchService = new BatchService(_missionService);
            
            // Инициализация локализации
            UILocalization.LanguageChanged += OnLanguageChanged;
            ApplyLocalization();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Инициализация UI Language комбобокса
            UILanguageCombo.SelectedIndex = 0; // English by default
            // Default radio source
            if (RadioSourceCombo != null)
                RadioSourceCombo.SelectedIndex = 0;
        }

        private void SetEnabled(bool enabled)
        {
            LocaleCombo.IsEnabled = enabled;
            LocaleIndexBox.IsEnabled = enabled;
            LocaleNameBox.IsEnabled = enabled;
            MissionNameBox.IsEnabled = enabled;
            MissionDescBox.IsEnabled = enabled;
            RedTaskBox.IsEnabled = enabled;
            BlueTaskBox.IsEnabled = enabled;
        }
n        private void LoadMission_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "DCS Mission (*.miz)|*.miz",
                Title = UILocalization.Get("Menu_File_Open")
            };
n            if (dlg.ShowDialog() != true)
                return;
n            try
            {
                CloseSession();
n                _session = _missionService.LoadMission(dlg.FileName);
n                LoadLocales();
                
                // Если доступно несколько локалей, спросим пользователя какую выбрать
                if (LocaleCombo.Items.Count > 1)
                {
                    var localeWindow = new Window
                    {
                        Title = UILocalization.Get("Dialog_SelectLocale_Title"),
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this
                    };
n                    var panel = new StackPanel { Margin = new Thickness(20) };
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = UILocalization.Get("Dialog_AvailableLocales"), 
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    var localeCombo = new ComboBox 
                    { 
                        Width = 360,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    
                    foreach (var item in LocaleCombo.Items)
                        localeCombo.Items.Add(item);
                    
                    localeCombo.SelectedIndex = 0;
                    panel.Children.Add(localeCombo);

                    var okButton = new Button 
                    { 
                        Content = UILocalization.Get("Button_OK"), 
                        Width = 100,
                        Height = 30
                    };
                    okButton.Click += (s, e) => localeWindow.DialogResult = true;
                    
                    panel.Children.Add(okButton);
                    localeWindow.Content = panel;
n                    if (localeWindow.ShowDialog() == true)
                    {
                        LocaleCombo.SelectedItem = localeCombo.SelectedItem;
                    }
                }
                
                LoadBriefingFields();
                SetEnabled(true);
                SideStatusText.Text = Path.GetFileName(dlg.FileName);
                StatusText.Text = string.Format(UILocalization.Get("Status_Loaded"), dlg.FileName + $" ({CurrentLocale})");
                // Show miz_id if present in mission
                if (_session?.Mission != null)
                {
                    var id = _session.Mission.MizId;
                    MizIdText.Text = string.IsNullOrWhiteSpace(id) ? string.Empty : string.Format(UILocalization.Get("Label_MizId"), id);
                }
            }
            catch (Exception ex)
            {
                SetEnabled(false);
                SideStatusText.Text = UILocalization.Get("Status_NoFile");
                StatusText.Text = UILocalization.Get("Status_Error");
                MessageBox.Show(ex.Message, UILocalization.Get("Status_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_session == null) return;
n                _missionService.Save(_session);
                StatusText.Text = UILocalization.Get("Status_Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, UILocalization.Get("Message_SaveError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsMiz_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            var dlg = new SaveFileDialog
            {
                Filter = "DCS Mission (*.miz)|*.miz",
                Title = UILocalization.Get("Menu_File_SaveAs")
            };
            if (dlg.ShowDialog() != true)
                return;
            try
            {
                _missionService.SaveAsMiz(_session, dlg.FileName);
                StatusText.Text = string.Format(UILocalization.Get("Status_Loaded"), dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, UILocalization.Get("Message_SaveError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            var dlg = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt|All files|*.*",
                Title = UILocalization.Get("Menu_File_SaveAsTxt")
            };
            if (dlg.ShowDialog() != true) return;
            var locale = (string?)LocaleCombo.SelectedItem ?? "DEFAULT";
            _missionService.ExportTxt(_session, locale, dlg.FileName);
            StatusText.Text = string.Format(UILocalization.Get("Status_Loaded"), dlg.FileName);
        }

        private void ImportTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            var dlg = new OpenFileDialog
            {
                Filter = "Text file (*.txt)|*.txt|All files|*.*",
                Title = UILocalization.Get("Menu_File_ImportTxt")
            };
            if (dlg.ShowDialog() != true) return;
            var locale = (string?)LocaleCombo.SelectedItem ?? "DEFAULT";
            _missionService.ImportTxt(_session, locale, dlg.FileName);
            StatusText.Text = $"TXT imported: {dlg.FileName}";
        }

        private void BatchImportTxt_Click(object sender, RoutedEventArgs e)
        {
            var folder = Interaction.InputBox(UILocalization.Get("Menu_Batch"), UILocalization.Get("Menu_Batch"), Environment.CurrentDirectory);
            if (string.IsNullOrWhiteSpace(folder)) return;
            var locale = Interaction.InputBox("Локаль для импорта:", "Пакетный импорт", "DEFAULT");
            if (string.IsNullOrWhiteSpace(locale)) return;
            var result = _batchService.BatchImportTxt(folder.Trim(), locale.Trim());
            MessageBox.Show(string.Join(Environment.NewLine, result), UILocalization.Get("Menu_Batch"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BatchSaveAsTxt_Click(object sender, RoutedEventArgs e)
        {
            var folder = Interaction.InputBox("Папка с .miz (туда же положим .txt):", "Пакетный экспорт", Environment.CurrentDirectory);
            if (string.IsNullOrWhiteSpace(folder)) return;
            var locale = Interaction.InputBox("Локаль для экспорта:", "Пакетный экспорт", "DEFAULT");
            if (string.IsNullOrWhiteSpace(locale)) return;
            var result = _batch_service.BatchSaveAsTxt(folder.Trim(), locale.Trim());
            MessageBox.Show(string.Join(Environment.NewLine, result), "Пакетный экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BatchStateAnalyze_Click(object sender, RoutedEventArgs e)
        {
            var folder = Interaction.InputBox("Папка с .miz:", "Пакетный анализ", Environment.CurrentDirectory);
            if (string.IsNullOrWhiteSpace(folder)) return;
            var result = _batch_service.BatchStateAnalyze(folder.Trim());
            MessageBox.Show(string.Join(Environment.NewLine, result), "Пакетный анализ", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void InfoAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("MizEdit — базовый макет. Добавлены заготовки пакетных операций и анализа.", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private string CurrentLocale => (string?)LocaleCombo.SelectedItem ?? "DEFAULT";

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.Mission == null) return;
            var locale = CurrentLocale;
            // Пишем в наиболее типовые ключи, учитывая dictionary.
            _session.Localization.SetBriefingString(_session.Mission, "name", MissionNameBox.Text, locale);
            _session.Localization.SetBriefingString(_session.Mission, "descriptionText", MissionDescBox.Text, locale);
            _session.Localization.SetBriefingString(_session.Mission, "descriptionRedTask", RedTaskBox.Text, locale);
            _session.Localization.SetBriefingString(_session.Mission, "descriptionBlueTask", BlueTaskBox.Text, locale);
            StatusText.Text = "Применено в памяти. Используйте 'Сохранить как', чтобы записать в .miz";
        }

        private void LoadBriefingFields()
        {
            if (_session?.Mission == null) return;
            var locale = CurrentLocale;
            MissionNameBox.Text = _session.Localization.ResolveBriefingString(_session.Mission, "name", locale);
            MissionDescBox.Text = _session.Localization.ResolveBriefingString(_session.Mission, "descriptionText", locale);
            RedTaskBox.Text = _session.Localization.ResolveBriefingString(_session.Mission, "descriptionRedTask", locale);
            BlueTaskBox.Text = _session.Localization.ResolveBriefingString(_session.Mission, "descriptionBlueTask", locale);
            
            LoadPictures();
            LoadKneeboard();
            LoadAudio();
            LoadTriggers();
            LoadRadio();
            LoadTriggerPic();
            LoadScripts();
        }

        private void LoadPictures()
        {
            if (_session?.Mission == null) return;
                        PicturesList.Items.Clear();
            var pictures = _session.Mission.GetPictureFileNames();
            var locale = CurrentLocale;
            var mapResource = _session.Localization.LoadMapResource(locale);
                        foreach (var pic in pictures)
            {                // Если это ResKey — резолвим через mapResource
                var resolved = pic;                if (pic.StartsWith("ResKey_", StringComparison.OrdinalIgnoreCase))
                {                    if (mapResource.TryGetValue(pic, out var fileName))                    {                        resolved = $"{pic} → {fileName}";                    }                }                // Если это обычный файл - показываем как есть
                PicturesList.Items.Add(resolved);            }
        }

        private void LoadAudio()
        {
            if (_session?.Mission == null) return;
                        AudioList.Items.Clear();
            var locale = CurrentLocale;
            var audioExtensions = new[] { ".ogg", ".wav", ".mp3" };
            var addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // 1. Сначала загружаем из mapResource (ResKey_Snd_ и ResKey_Action_)
            var mapResource = _session.Localization.LoadMapResource(locale);
                        foreach (var kv in mapResource)
            {                if (kv.Key.StartsWith("ResKey_Snd_", StringComparison.OrdinalIgnoreCase) ||                    kv.Key.StartsWith("ResKey_Action_", StringComparison.OrdinalIgnoreCase))                {                    var ext = Path.GetExtension(kv.Value).ToLowerInvariant();                    if (audioExtensions.Contains(ext))                    {                        AudioList.Items.Add($"{kv.Key} → {kv.Value}");                        addedFiles.Add(kv.Value);                    }                }            }
            // 2. Сканируем реальную папку l10n/{locale}/ для аудио файлов
            var audioDir = Path.Combine(_session.Archive.WorkDir, "l10n", locale);
            if (Directory.Exists(audioDir))
            {                var audioFiles = Directory.GetFiles(audioDir)                    .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))                    .Select(f => Path.GetFileName(f))                    .OrderBy(f => f);
                foreach (var audioFile in audioFiles)                {                    if (!addedFiles.Contains(audioFile))                    {                        AudioList.Items.Add(audioFile);                        addedFiles.Add(audioFile);                    }                }            }
            // 3. Fallback на DEFAULT, если текущая локаль пустая
            if (AudioList.Items.Count == 0 && !locale.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
            {                var defaultAudioDir = Path.Combine(_session.Archive.WorkDir, "l10n", "DEFAULT");                if (Directory.Exists(defaultAudioDir))                {                    var audioFiles = Directory.GetFiles(defaultAudioDir)                        .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))                        .Select(f => Path.GetFileName(f))                        .OrderBy(f => f);
                    foreach (var audioFile in audioFiles)                    {                        AudioList.Items.Add($"[DEFAULT] {audioFile}");                    }                }            }        }

        private void LoadKneeboard()
        {
            if (_session?.Archive == null) return;
                        KneeboardList.Items.Clear();
            var kneeboardPath = Path.Combine(_session.Archive.WorkDir, "KNEEBOARD", "IMAGES");
            
