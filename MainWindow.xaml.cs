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

        public MainWindow()
        {
            InitializeComponent();
            SetEnabled(false);
            _batchService = new BatchService(_missionService);
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

        private void LoadMission_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "DCS Mission (*.miz)|*.miz",
                Title = "Open .miz"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                CloseSession();

                _session = _missionService.LoadMission(dlg.FileName);

                LoadLocales();
                
                // Если доступно несколько локалей, спросим пользователя какую выбрать
                if (LocaleCombo.Items.Count > 1)
                {
                    var localeWindow = new Window
                    {
                        Title = "Select Mission Language / 选择任务语言 / Выберите язык миссии",
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this
                    };

                    var panel = new StackPanel { Margin = new Thickness(20) };
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = "Available languages:", 
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
                        Content = "OK", 
                        Width = 100,
                        Height = 30
                    };
                    okButton.Click += (s, e) => localeWindow.DialogResult = true;
                    
                    panel.Children.Add(okButton);
                    localeWindow.Content = panel;

                    if (localeWindow.ShowDialog() == true)
                    {
                        LocaleCombo.SelectedItem = localeCombo.SelectedItem;
                    }
                }
                
                LoadBriefingFields();

                SetEnabled(true);
                SideStatusText.Text = Path.GetFileName(dlg.FileName);
                StatusText.Text = $"Loaded: {dlg.FileName} ({CurrentLocale})";
            }
            catch (Exception ex)
            {
                SetEnabled(false);
                SideStatusText.Text = "No File Opened";
                StatusText.Text = "Error";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_session == null) return;

                _missionService.Save(_session);
                StatusText.Text = "Saved to original .miz";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsMiz_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "DCS Mission (*.miz)|*.miz",
                Title = "Save As"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                _missionService.SaveAsMiz(_session, dlg.FileName);
                StatusText.Text = $"Saved: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt|All files|*.*",
                Title = "Save briefing as TXT"
            };

            if (dlg.ShowDialog() != true) return;

            var locale = (string?)LocaleCombo.SelectedItem ?? "DEFAULT";
            _missionService.ExportTxt(_session, locale, dlg.FileName);
            StatusText.Text = $"TXT saved: {dlg.FileName}";
        }

        private void ImportTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Text file (*.txt)|*.txt|All files|*.*",
                Title = "Import TXT"
            };

            if (dlg.ShowDialog() != true) return;

            var locale = (string?)LocaleCombo.SelectedItem ?? "DEFAULT";
            _missionService.ImportTxt(_session, locale, dlg.FileName);
            StatusText.Text = $"TXT imported: {dlg.FileName}";
        }

        private void BatchImportTxt_Click(object sender, RoutedEventArgs e)
        {
            var folder = Interaction.InputBox("Папка с .miz и .txt (одинаковое имя):", "Batch Import", Environment.CurrentDirectory);
            if (string.IsNullOrWhiteSpace(folder)) return;

            var locale = Interaction.InputBox("Locale для импорта:", "Batch Import", "DEFAULT");
            if (string.IsNullOrWhiteSpace(locale)) return;

            var result = _batchService.BatchImportTxt(folder.Trim(), locale.Trim());
            MessageBox.Show(string.Join(Environment.NewLine, result), "Batch Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BatchSaveAsTxt_Click(object sender, RoutedEventArgs e)
        {
            var folder = Interaction.InputBox("Папка с .miz (туда же положим .txt):", "Batch Export", Environment.CurrentDirectory);
            if (string.IsNullOrWhiteSpace(folder)) return;

            var locale = Interaction.InputBox("Locale для экспорта:", "Batch Export", "DEFAULT");
            if (string.IsNullOrWhiteSpace(locale)) return;

            var result = _batchService.BatchSaveAsTxt(folder.Trim(), locale.Trim());
            MessageBox.Show(string.Join(Environment.NewLine, result), "Batch Save", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BatchStateAnalyze_Click(object sender, RoutedEventArgs e)
        {
            var folder = Interaction.InputBox("Папка с .miz:", "Batch Analyze", Environment.CurrentDirectory);
            if (string.IsNullOrWhiteSpace(folder)) return;

            var result = _batchService.BatchStateAnalyze(folder.Trim());
            MessageBox.Show(string.Join(Environment.NewLine, result), "Batch Analyze", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void InfoAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("MizEdit MVP layout. Batch/analysis stubs added.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
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

            StatusText.Text = "Applied in memory. Use Save As to write .miz";
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
            {
                // Если это ResKey — резолвим через mapResource
                var resolved = pic;
                if (pic.StartsWith("ResKey_", StringComparison.OrdinalIgnoreCase))
                {
                    if (mapResource.TryGetValue(pic, out var fileName))
                    {
                        resolved = $"{pic} → {fileName}";
                    }
                }
                // Если это обычный файл - показываем как есть
                PicturesList.Items.Add(resolved);
            }
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
            {
                if (kv.Key.StartsWith("ResKey_Snd_", StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.StartsWith("ResKey_Action_", StringComparison.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(kv.Value).ToLowerInvariant();
                    if (audioExtensions.Contains(ext))
                    {
                        AudioList.Items.Add($"{kv.Key} → {kv.Value}");
                        addedFiles.Add(kv.Value);
                    }
                }
            }

            // 2. Сканируем реальную папку l10n/{locale}/ для аудио файлов
            var audioDir = Path.Combine(_session.Archive.WorkDir, "l10n", locale);
            if (Directory.Exists(audioDir))
            {
                var audioFiles = Directory.GetFiles(audioDir)
                    .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(f => Path.GetFileName(f))
                    .OrderBy(f => f);

                foreach (var audioFile in audioFiles)
                {
                    if (!addedFiles.Contains(audioFile))
                    {
                        AudioList.Items.Add(audioFile);
                        addedFiles.Add(audioFile);
                    }
                }
            }

            // 3. Fallback на DEFAULT, если текущая локаль пустая
            if (AudioList.Items.Count == 0 && !locale.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                var defaultAudioDir = Path.Combine(_session.Archive.WorkDir, "l10n", "DEFAULT");
                if (Directory.Exists(defaultAudioDir))
                {
                    var audioFiles = Directory.GetFiles(defaultAudioDir)
                        .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .Select(f => Path.GetFileName(f))
                        .OrderBy(f => f);

                    foreach (var audioFile in audioFiles)
                    {
                        AudioList.Items.Add($"[DEFAULT] {audioFile}");
                    }
                }
            }
        }

        private void LoadKneeboard()
        {
            if (_session?.Archive == null) return;
            
            KneeboardList.Items.Clear();
            var kneeboardPath = Path.Combine(_session.Archive.WorkDir, "KNEEBOARD", "IMAGES");
            
            if (!Directory.Exists(kneeboardPath))
                return;
            
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
            var files = Directory.GetFiles(kneeboardPath)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .Select(f => Path.GetFileName(f))
                .ToList();
            
            foreach (var file in files)
            {
                KneeboardList.Items.Add(file);
            }
        }

        private void LoadTriggers()
        {
            if (_session?.Mission == null) return;
            
            TriggersList.Items.Clear();
            
            // Читаем триггеры из mission.trig
            var triggers = _session.Mission.GetTriggers();
            foreach (var trig in triggers)
            {
                TriggersList.Items.Add(trig);
            }

            // Добавляем разделитель
            if (triggers.Count > 0)
            {
                TriggersList.Items.Add("--- TRIGGER RULES ---");
            }

            // Читаем правила триггеров из mission.trigrules
            var rules = _session.Mission.GetTrigRules();
            foreach (var rule in rules)
            {
                TriggersList.Items.Add(rule);
            }
        }

        private void LoadRadio()
        {
            if (_session?.Mission == null) return;
            
            RadioList.Items.Clear();
            
            try
            {
                var transmissions = _session.Mission.GetRadioTransmissions();
                
                RadioStatusText.Text = $"Found {transmissions.Count} radio messages";
                RadioStatusText.Foreground = System.Windows.Media.Brushes.Blue;
                
                if (transmissions.Count == 0)
                {
                    RadioList.Items.Add("(No radio messages found)");
                    return;
                }
                
                foreach (var trans in transmissions)
                {
                    string displayText = $"{trans.GroupName} - Task {trans.TaskIndex} (Action {trans.ActionIndex})";
                    RadioList.Items.Add(displayText);
                }
                
                // Clear editor fields
                RadioSubtitleKeyBox.Text = "";
                RadioFileKeyBox.Text = "";
                RadioDurationBox.Text = "";
                RadioSubtitleTextBox.Text = "";
                _selectedRadioMessage = null;
            }
            catch (Exception ex)
            {
                RadioStatusText.Text = $"Error: {ex.Message}";
                RadioStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show($"Error loading radio messages:\n\n{ex.Message}\n\nStack:\n{ex.StackTrace}", "Debug", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTriggerPic()
        {
            if (_session?.Mission == null) return;
            
            TriggerPicsList.Items.Clear();
            
            var trigPics = _session.Mission.GetTriggerPictures();
            foreach (var pic in trigPics)
            {
                TriggerPicsList.Items.Add(pic);
            }
        }

        private void LoadScripts()
        {
            if (_session?.Archive == null) return;
            
            ScriptCombo.Items.Clear();
            ScriptBox.Text = string.Empty;
            ScriptStatusText.Text = string.Empty;
            
            var scriptPaths = new List<string>();
            var workDir = _session.Archive.WorkDir;
            
            // Скрипты из Scripts/World/
            var worldScripts = Path.Combine(workDir, "Scripts", "World");
            if (Directory.Exists(worldScripts))
            {
                scriptPaths.AddRange(Directory.GetFiles(worldScripts, "*.lua", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(workDir, f)));
            }
            
            // Скрипты из l10n/{locale}/
            var locale = CurrentLocale;
            var localeScripts = Path.Combine(workDir, "l10n", locale);
            if (Directory.Exists(localeScripts))
            {
                scriptPaths.AddRange(Directory.GetFiles(localeScripts, "*.lua", SearchOption.TopDirectoryOnly)
                    .Select(f => Path.GetRelativePath(workDir, f)));
            }
            
            foreach (var script in scriptPaths.OrderBy(s => s))
            {
                ScriptCombo.Items.Add(script);
            }
            
            if (ScriptCombo.Items.Count > 0)
            {
                ScriptCombo.SelectedIndex = 0;
            }
        }

        private void ScriptCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_session?.Archive == null || ScriptCombo.SelectedItem is not string scriptPath) return;
            
            try
            {
                var fullPath = Path.Combine(_session.Archive.WorkDir, scriptPath);
                if (File.Exists(fullPath))
                {
                    ScriptBox.Text = File.ReadAllText(fullPath);
                    ScriptStatusText.Text = $"Loaded: {scriptPath}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadScripts_Click(object sender, RoutedEventArgs e)
        {
            LoadScripts();
            StatusText.Text = "Scripts reloaded";
        }

        private void SaveScript_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.Archive == null || ScriptCombo.SelectedItem is not string scriptPath) return;
            
            try
            {
                var fullPath = Path.Combine(_session.Archive.WorkDir, scriptPath);
                File.WriteAllText(fullPath, ScriptBox.Text);
                ScriptStatusText.Text = $"Saved: {scriptPath}";
                StatusText.Text = "Script saved (use Save As Miz to write to .miz)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadLocales()
        {
            if (_session == null) return;

            LocaleCombo.Items.Clear();

            var locales = _session.Localization.GetLocales();
            foreach (var loc in locales.Distinct(StringComparer.OrdinalIgnoreCase))
                LocaleCombo.Items.Add(loc);

            LocaleCombo.SelectedItem = locales.FirstOrDefault() ?? "DEFAULT";
            LocaleIndexBox.Text = (LocaleCombo.SelectedIndex + 1).ToString();
        }

        private void AddLocale_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;

            var locale = (LocaleNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(locale)) return;

            _session.Localization.AddLocale(locale);

            LoadLocales();
            LocaleCombo.SelectedItem = locale;

            StatusText.Text = $"Locale created: {locale}";
        }

        private void DeleteLocale_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) return;
            if (LocaleCombo.SelectedItem is not string locale) return;
            if (locale.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase)) return;

            _session.Localization.DeleteLocale(locale);
            LoadLocales();
            StatusText.Text = $"Locale deleted: {locale}";
        }

        private void LocaleUp_Click(object sender, RoutedEventArgs e)
        {
            if (LocaleCombo.Items.Count == 0) return;
            var idx = Math.Max(0, LocaleCombo.SelectedIndex - 1);
            LocaleCombo.SelectedIndex = idx;
            LocaleIndexBox.Text = (idx + 1).ToString();
        }

        private void LocaleDown_Click(object sender, RoutedEventArgs e)
        {
            if (LocaleCombo.Items.Count == 0) return;
            var idx = Math.Min(LocaleCombo.Items.Count - 1, LocaleCombo.SelectedIndex + 1);
            LocaleCombo.SelectedIndex = idx;
            LocaleIndexBox.Text = (idx + 1).ToString();
        }

        private void LocaleCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LocaleCombo.SelectedIndex >= 0)
                LocaleIndexBox.Text = (LocaleCombo.SelectedIndex + 1).ToString();

            // При смене locale обновляем поля брифинга (используем dictionary при наличии)
            LoadBriefingFields();
        }

        private void CloseSession()
        {
            StopAudio_Click(null, null);
            _session?.Dispose();
            _session = null;
            SideStatusText.Text = "No File Opened";
            StatusText.Text = "Ready";
            LocaleCombo.Items.Clear();
            LocaleIndexBox.Text = string.Empty;
            MissionNameBox.Clear();
            MissionDescBox.Clear();
            RedTaskBox.Clear();
            BlueTaskBox.Clear();
        }

        private void AudioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AudioList.SelectedItem is string selected)
            {
                // Поддерживаем форматы:
                // 1. "ResKey_Snd_Radio0 → Radio0.ogg"
                // 2. "Radio0.ogg" (прямой файл)
                // 3. "[DEFAULT] Radio0.ogg" (из DEFAULT папки)
                
                if (selected.Contains("→"))
                {
                    var parts = selected.Split('→');
                    _selectedAudioFile = parts[1].Trim();
                }
                else if (selected.StartsWith("[DEFAULT] "))
                {
                    _selectedAudioFile = selected.Substring("[DEFAULT] ".Length).Trim();
                }
                else
                {
                    _selectedAudioFile = selected.Trim();
                }
            }
        }

        private void PlayAudio_Click(object? sender, RoutedEventArgs? e)
        {
            if (_session == null || string.IsNullOrWhiteSpace(_selectedAudioFile)) return;

            try
            {
                var locale = CurrentLocale;
                var workDir = _session.Archive.WorkDir;
                
                // Ищем аудио файл в нескольких местах
                var searchPaths = new[]
                {
                    Path.Combine(workDir, "l10n", locale, _selectedAudioFile),
                    Path.Combine(workDir, "l10n", "DEFAULT", _selectedAudioFile),
                    Path.Combine(workDir, _selectedAudioFile)
                };

                string? audioPath = null;
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        audioPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(audioPath))
                {
                    MessageBox.Show($"Audio file not found: {_selectedAudioFile}\n\nSearched in:\n" + 
                        string.Join("\n", searchPaths), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StopAudio_Click(null, null);

                _vorbisReader = new VorbisWaveReader(audioPath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_vorbisReader);
                _waveOut.Play();

                StatusText.Text = $"Playing: {_selectedAudioFile}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing audio: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopAudio_Click(object? sender, RoutedEventArgs? e)
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            
            if (_vorbisReader != null)
            {
                _vorbisReader.Dispose();
                _vorbisReader = null;
            }
            
            if (sender != null)
            {
                StatusText.Text = "Audio stopped";
            }
        }

        private string? _selectedKneeboardFile;

        private void KneeboardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KneeboardList.SelectedItem is string selected)
            {
                _selectedKneeboardFile = selected;
                DisplayKneeboardPreview();
            }
        }

        private void DisplayKneeboardPreview()
        {
            if (_session == null || string.IsNullOrWhiteSpace(_selectedKneeboardFile))
            {
                KneeboardViewer.Source = null;
                return;
            }

            try
            {
                var kneeboardPath = Path.Combine(_session.Archive.WorkDir, "KNEEBOARD", "IMAGES", _selectedKneeboardFile);

                if (!File.Exists(kneeboardPath))
                {
                    KneeboardViewer.Source = null;
                    StatusText.Text = $"File not found: {_selectedKneeboardFile}";
                    return;
                }

                // Загружаем изображение в Image элемент
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(kneeboardPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                KneeboardViewer.Source = bitmap;

                StatusText.Text = $"Viewing: {_selectedKneeboardFile}";
            }
            catch (Exception ex)
            {
                KneeboardViewer.Source = null;
                StatusText.Text = $"Error loading kneeboard: {ex.Message}";
            }
        }

        private string? _selectedPictureFile;

        private void PicturesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PicturesList.SelectedItem is string selected)
            {
                _selectedPictureFile = selected;
                DisplayPicturePreview();
            }
        }

        private void DisplayPicturePreview()
        {
            if (_session == null || string.IsNullOrWhiteSpace(_selectedPictureFile))
            {
                PictureViewer.Source = null;
                return;
            }

            try
            {
                var locale = CurrentLocale;
                var pictureName = _selectedPictureFile;

                // Если это ResKey, извлекаем имя файла
                if (pictureName.Contains("→"))
                {
                    var parts = pictureName.Split('→');
                    pictureName = parts[1].Trim();
                }

                var fileName = Path.GetFileName(pictureName);
                var workDir = _session.Archive.WorkDir;
                
                // Ищем файл в нескольких местах (по приоритету)
                var searchPaths = new[]
                {
                    Path.Combine(workDir, "l10n", locale, fileName),
                    Path.Combine(workDir, "l10n", "DEFAULT", fileName),
                    Path.Combine(workDir, "l10n", locale, pictureName),
                    Path.Combine(workDir, fileName),
                };

                string? foundPath = null;
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(foundPath))
                {
                    PictureViewer.Source = null;
                    StatusText.Text = $"Picture not found: {fileName}";
                    return;
                }

                // Загружаем изображение в Image элемент
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(foundPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                PictureViewer.Source = bitmap;
                
                StatusText.Text = $"Viewing: {fileName}";
            }
            catch (Exception ex)
            {
                PictureViewer.Source = null;
                StatusText.Text = $"Error loading picture: {ex.Message}";
            }
        }


        private void AddPicture_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.Mission == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title = "Add Picture",
                Multiselect = true
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var locale = CurrentLocale;
                var targetDir = Path.Combine(_session.Archive.WorkDir, "l10n", locale);
                Directory.CreateDirectory(targetDir);

                var pictures = _session.Mission.GetPictureFileNames();

                foreach (var sourceFile in dlg.FileNames)
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var targetPath = Path.Combine(targetDir, fileName);
                    
                    // Копируем файл в l10n/{locale}/
                    File.Copy(sourceFile, targetPath, overwrite: true);

                    // Добавляем в миссию, если еще нет
                    if (!pictures.Any(p => p.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        pictures.Add(fileName);
                    }
                }

                // Сохраняем обновленный список в mission
                _session.Mission.AddPictures(pictures);

                LoadPictures();
                StatusText.Text = $"Added {dlg.FileNames.Length} picture(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding pictures: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemovePicture_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.Mission == null || PicturesList.SelectedItem is not string picture)
            {
                MessageBox.Show("Выберите изображение для удаления", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (MessageBox.Show($"Удалить '{picture}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Извлекаем имя файла если это ResKey
                var pictureName = picture;
                if (picture.Contains("→"))
                {
                    var parts = picture.Split('→');
                    pictureName = parts[1].Trim();
                }

                _session.Mission.RemovePicture(pictureName);
                PicturesList.Items.Remove(picture);
                PictureViewer.Source = null;
                StatusText.Text = $"Removed picture: {picture}";
            }
        }

        // Add/Remove functions for Kneeboard
        private void AddKneeboard_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.Archive == null) return;

            var dlg = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title = "Add Kneeboard Image",
                Multiselect = true
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var kneeboardDir = Path.Combine(_session.Archive.WorkDir, "KNEEBOARD", "IMAGES");
                Directory.CreateDirectory(kneeboardDir);

                foreach (var sourceFile in dlg.FileNames)
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var targetPath = Path.Combine(kneeboardDir, fileName);
                    
                    // Копируем файл в KNEEBOARD/IMAGES/
                    File.Copy(sourceFile, targetPath, overwrite: true);
                }

                LoadKneeboard();
                StatusText.Text = $"Added {dlg.FileNames.Length} kneeboard image(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding kneeboard images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveKneeboard_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || KneeboardList.SelectedItem is not string kneeboard)
            {
                MessageBox.Show("Выберите изображение для удаления", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (MessageBox.Show($"Удалить '{kneeboard}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    var kneeboardPath = Path.Combine(_session.Archive.WorkDir, "KNEEBOARD", "IMAGES", kneeboard);
                    if (File.Exists(kneeboardPath))
                    {
                        File.Delete(kneeboardPath);
                    }
                    
                    KneeboardList.Items.Remove(kneeboard);
                    KneeboardViewer.Source = null;
                    StatusText.Text = $"Removed kneeboard: {kneeboard}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing kneeboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Add/Remove functions for Triggers
        private void AddTrigger_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Добавление триггеров требует сложного редактора условий и действий.\nЭта функция будет реализована в будущих версиях.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (TriggersList.SelectedItem is not string trigger || _session?.Mission == null)
            {
                MessageBox.Show("Выберите триггер для удаления", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (MessageBox.Show($"Удалить триггер?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                TriggersList.Items.Remove(trigger);
                LoadTriggers(); // Перезагружаем для обновления
                StatusText.Text = "Removed trigger";
            }
        }

        // Add/Remove functions for Radio
        private void RefreshRadio_Click(object sender, RoutedEventArgs e)
        {
            LoadRadio();
            RadioStatusText.Text = "Radio messages refreshed";
        }

        private void RadioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_session?.Mission == null || RadioList.SelectedIndex < 0) return;
            
            var transmissions = _session.Mission.GetRadioTransmissions();
            if (RadioList.SelectedIndex >= transmissions.Count) return;
            
            _selectedRadioMessage = transmissions[RadioList.SelectedIndex];
            
            // Populate editor fields
            RadioSubtitleKeyBox.Text = _selectedRadioMessage.SubtitleKey;
            RadioFileKeyBox.Text = _selectedRadioMessage.FileKey;
            RadioDurationBox.Text = _selectedRadioMessage.Duration.ToString();
            
            // Resolve subtitle text from dictionary
            string locale = LocaleCombo.Text;
            string? subtitleText = _session.Localization.GetDictionaryValue(locale, _selectedRadioMessage.SubtitleKey);
            
            if (string.IsNullOrEmpty(subtitleText))
            {
                // Fallback to DEFAULT locale
                subtitleText = _session.Localization.GetDictionaryValue("DEFAULT", _selectedRadioMessage.SubtitleKey);
            }
            
            RadioSubtitleTextBox.Text = subtitleText ?? $"[Key not found: {_selectedRadioMessage.SubtitleKey}]";
            RadioStatusText.Text = "";
        }

        private void SaveRadioSubtitle_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.Mission == null || _selectedRadioMessage == null)
            {
                MessageBox.Show("Select a radio message first", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            string newText = RadioSubtitleTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newText))
            {
                MessageBox.Show("Subtitle text cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string locale = LocaleCombo.Text;
            
            try
            {
                _session.Localization.UpdateDictionaryEntry(locale, _selectedRadioMessage.SubtitleKey, newText);
                RadioStatusText.Text = $"✓ Saved to {locale} dictionary";
                RadioStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RadioStatusText.Text = "✗ Save failed";
                RadioStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        // Add/Remove functions for TriggerPic
        private void AddTriggerPic_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Добавление снимков триггеров требует сложного редактора.\nЭта функция будет реализована в будущих версиях.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveTriggerPic_Click(object sender, RoutedEventArgs e)
        {
            if (TriggerPicsList.SelectedItem is not string pic)
            {
                MessageBox.Show("Выберите снимок для удаления", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (MessageBox.Show($"Удалить снимок?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                TriggerPicsList.Items.Remove(pic);
                LoadTriggerPic(); // Перезагружаем для обновления
                StatusText.Text = "Removed trigger pic";
            }
        }
    }
}
