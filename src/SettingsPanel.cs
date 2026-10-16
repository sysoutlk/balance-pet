// Modern settings card (dark, rounded, custom switches/sliders) built from an inline
// XAML template with single-quoted attributes so the source stays readable in C# 5.
// Provider rows and key inputs are generated in code because the provider list is data.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DshPet
{
    internal sealed class SettingsPanel : Window
    {
        private const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        WindowStyle='None' AllowsTransparency='True' Background='{x:Null}' Topmost='True'
        ShowInTaskbar='False' SizeToContent='WidthAndHeight' ResizeMode='NoResize'
        FontFamily='Microsoft YaHei UI, Segoe UI' TextOptions.TextFormattingMode='Display'>
  <Window.Resources>
    <Style x:Key='Btn' TargetType='Button'>
      <Setter Property='Foreground' Value='#E6ECFF'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='Margin' Value='0,0,7,7'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='BorderBrush' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='#252D48' CornerRadius='9' Padding='11,0'
                    BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#33406A'/></Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#4B6BFB'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Mini' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Height' Value='26'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Margin' Value='0'/>
      <Setter Property='Padding' Value='0'/>
    </Style>
    <Style x:Key='Accent' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Foreground' Value='#FFFFFF'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='#4B6BFB' CornerRadius='9' Padding='11,0'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#5E7BFF'/></Trigger>
              <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#3A55D8'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Switch' TargetType='CheckBox'>
      <Setter Property='Foreground' Value='#C9D4EE'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Margin' Value='0,5,16,5'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='CheckBox'>
            <StackPanel Orientation='Horizontal' Background='Transparent'>
              <Border x:Name='track' Width='34' Height='19' CornerRadius='10' Background='#2A3350'>
                <Ellipse x:Name='knob' Width='13' Height='13' Fill='#8FA0C8' HorizontalAlignment='Left' Margin='3,0,0,0'/>
              </Border>
              <ContentPresenter Margin='8,0,0,0' VerticalAlignment='Center'/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='track' Property='Background' Value='#4B6BFB'/>
                <Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/>
                <Setter TargetName='knob' Property='Margin' Value='0,0,3,0'/>
                <Setter TargetName='knob' Property='Fill' Value='#FFFFFF'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Flat' TargetType='Slider'>
      <Setter Property='Height' Value='24'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Slider'>
            <Grid Background='Transparent'>
              <Border Height='4' CornerRadius='2' Background='#2A3350' VerticalAlignment='Center'/>
              <Track x:Name='PART_Track'>
                <Track.DecreaseRepeatButton>
                  <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>
                    <RepeatButton.Template>
                      <ControlTemplate TargetType='RepeatButton'>
                        <Border Height='4' CornerRadius='2' Background='#4B6BFB' VerticalAlignment='Center'/>
                      </ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                  <Thumb Width='14' Height='14' Focusable='False' Cursor='Hand'>
                    <Thumb.Template>
                      <ControlTemplate TargetType='Thumb'>
                        <Ellipse Fill='#FFFFFF' Stroke='#4B6BFB' StrokeThickness='3'/>
                      </ControlTemplate>
                    </Thumb.Template>
                  </Thumb>
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                  <RepeatButton Command='Slider.IncreaseLarge' Focusable='False'>
                    <RepeatButton.Template>
                      <ControlTemplate TargetType='RepeatButton'>
                        <Border Background='Transparent'/>
                      </ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.IncreaseRepeatButton>
              </Track>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Sect' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Foreground' Value='#8FA0C8'/>
      <Setter Property='Margin' Value='1,16,0,7'/>
    </Style>
    <Style x:Key='Val' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Foreground' Value='#C9D4EE'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='Margin' Value='8,0,0,0'/>
    </Style>
    <Style x:Key='Lbl' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#C9D4EE'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style x:Key='KeyBox' TargetType='PasswordBox'>
      <Setter Property='Height' Value='26'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Foreground' Value='#E6ECFF'/>
      <Setter Property='CaretBrush' Value='White'/>
      <Setter Property='Background' Value='#121828'/>
      <Setter Property='BorderBrush' Value='#2A3350'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Padding' Value='6,0'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
    </Style>
    <Style x:Key='TxtBox' TargetType='TextBox'>
      <Setter Property='Height' Value='26'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Foreground' Value='#E6ECFF'/>
      <Setter Property='CaretBrush' Value='White'/>
      <Setter Property='Background' Value='#121828'/>
      <Setter Property='BorderBrush' Value='#2A3350'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Padding' Value='6,0'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
    </Style>
  </Window.Resources>
  <Border Background='#F2171C2B' CornerRadius='16' BorderBrush='#2EFFFFFF' BorderThickness='1' Padding='18,13,18,15'>
    <Border.Effect>
      <DropShadowEffect BlurRadius='26' ShadowDepth='6' Opacity='0.5' Color='#000000'/>
    </Border.Effect>
    <ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' MaxHeight='640'>
      <ScrollViewer.Resources>
        <Style TargetType='ScrollBar'>
          <Setter Property='Width' Value='6'/>
          <Setter Property='Background' Value='Transparent'/>
          <Setter Property='Template'>
            <Setter.Value>
              <ControlTemplate TargetType='ScrollBar'>
                <Grid Background='Transparent'>
                  <Track x:Name='PART_Track' IsDirectionReversed='True'>
                    <Track.DecreaseRepeatButton>
                      <RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/>
                    </Track.DecreaseRepeatButton>
                    <Track.Thumb>
                      <Thumb>
                        <Thumb.Template>
                          <ControlTemplate TargetType='Thumb'>
                            <Border Background='#3A4565' CornerRadius='3'/>
                          </ControlTemplate>
                        </Thumb.Template>
                      </Thumb>
                    </Track.Thumb>
                    <Track.IncreaseRepeatButton>
                      <RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/>
                    </Track.IncreaseRepeatButton>
                  </Track>
                </Grid>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
      </ScrollViewer.Resources>
      <StackPanel Width='394'>
        <DockPanel x:Name='Header' Background='Transparent'>
          <Button x:Name='CloseBtn' DockPanel.Dock='Right' Content='✕' Width='26' Height='26' Margin='0' FontSize='11' Style='{StaticResource Btn}'/>
          <StackPanel Margin='0,2,0,0'>
            <TextBlock Text='DSH 桌宠' FontSize='15' FontWeight='Bold' Foreground='#EAF0FF'/>
            <TextBlock x:Name='SubText' Text='…' FontSize='10' Foreground='#8FA0C8' Margin='0,3,0,0'/>
          </StackPanel>
        </DockPanel>
        <TextBlock Style='{StaticResource Sect}' Text='动作点播'/>
        <UniformGrid x:Name='ActionGrid' Columns='4'/>
        <CheckBox x:Name='AutoActionsChk' Style='{StaticResource Switch}' Content='随机动作' Margin='0,7,0,0'/>
        <TextBlock Style='{StaticResource Sect}' Text='外观'/>
        <WrapPanel x:Name='RolePanel'/>
        <Grid Margin='0,9,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='大小'/>
          <Slider x:Name='ScaleSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.4' Maximum='2.6'/>
          <TextBlock x:Name='ScaleVal' Grid.Column='2' Style='{StaticResource Val}' Text='1.00x'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='不透明度'/>
          <Slider x:Name='OpacitySlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.4' Maximum='1'/>
          <TextBlock x:Name='OpacityVal' Grid.Column='2' Style='{StaticResource Val}' Text='100%'/>
        </Grid>
        <TextBlock Style='{StaticResource Sect}' Text='行为'/>
        <WrapPanel>
          <CheckBox x:Name='WalkChk' Style='{StaticResource Switch}' Content='散步'/>
          <CheckBox x:Name='SnapChk' Style='{StaticResource Switch}' Content='贴边'/>
          <CheckBox x:Name='SleepChk' Style='{StaticResource Switch}' Content='作息'/>
          <CheckBox x:Name='QuipsChk' Style='{StaticResource Switch}' Content='台词'/>
        </WrapPanel>
        <DockPanel Margin='0,4,0,0'>
          <CheckBox x:Name='EdgeHideChk' Style='{StaticResource Switch}' Content='边缘隐藏' Margin='0,0,10,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='收起前等待'/>
          <TextBox x:Name='EdgeDelayBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='秒' Margin='5,0,0,0'/>
        </DockPanel>
        <Grid Margin='0,6,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='速度'/>
          <Slider x:Name='SpeedSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='25' Maximum='170'/>
          <TextBlock x:Name='SpeedVal' Grid.Column='2' Style='{StaticResource Val}' Text='72'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='动作间隔'/>
          <Slider x:Name='ActionPauseSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='6' Maximum='90'/>
          <TextBlock x:Name='ActionPauseVal' Grid.Column='2' Style='{StaticResource Val}' Text='14s'/>
        </Grid>
        <TextBlock Style='{StaticResource Sect}' Text='数据源（余额 API）'/>
        <StackPanel x:Name='ProviderList'/>
        <TextBlock Style='{StaticResource Sect}' Text='其他平台' Margin='1,4,0,6'/>
        <WrapPanel x:Name='UnsupportedPanel'/>
        <DockPanel Margin='0,6,0,0'>
          <Button x:Name='RefreshBtn' DockPanel.Dock='Right' Content='刷新余额' Width='88' Style='{StaticResource Btn}' Margin='0'/>
          <TextBlock x:Name='DataText' Style='{StaticResource Lbl}' Text='…' TextTrimming='CharacterEllipsis'/>
        </DockPanel>
        <TextBlock Style='{StaticResource Sect}' Text='套餐用量（5 小时 / 7 天窗口）'/>
        <StackPanel x:Name='PlanList'/>
        <TextBlock Style='{StaticResource Sect}' Text='提醒'/>
        <WrapPanel>
          <CheckBox x:Name='AlertChk' Style='{StaticResource Switch}' Content='余额预警'/>
          <CheckBox x:Name='BudgetChk' Style='{StaticResource Switch}' Content='预算提醒'/>
        </WrapPanel>
        <DockPanel Margin='0,4,0,0'>
          <TextBlock Style='{StaticResource Lbl}' Text='余额低于' Width='56'/>
          <TextBox x:Name='AlertBelowBox' Style='{StaticResource TxtBox}' Width='66'/>
          <TextBlock Style='{StaticResource Lbl}' Text='今日预算' Width='56' Margin='12,0,0,0'/>
          <TextBox x:Name='BudgetBox' Style='{StaticResource TxtBox}' Width='66'/>
          <TextBlock Style='{StaticResource Lbl}' Text='0 或留空 = 关闭' Margin='10,0,0,0' FontSize='10' Foreground='#7D8BB0'/>
        </DockPanel>
        <TextBlock Style='{StaticResource Sect}' Text='分身'/>
        <DockPanel Margin='0,0,0,6'>
          <CheckBox x:Name='CloneAutoChk' Style='{StaticResource Switch}' Content='自动分裂' Margin='0,0,10,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='上限'/>
          <TextBox x:Name='CloneMaxBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='只（0 = 不限）' Margin='5,0,0,0'/>
        </DockPanel>
        <DockPanel>
          <StackPanel DockPanel.Dock='Right' Orientation='Horizontal'>
            <Button x:Name='CloneAddBtn' Content='来一个' Width='72' Style='{StaticResource Btn}'/>
            <Button x:Name='CloneRemoveBtn' Content='收一个' Width='72' Margin='0' Style='{StaticResource Btn}'/>
          </StackPanel>
          <TextBlock x:Name='CloneCount' Style='{StaticResource Lbl}' Text='1 只'/>
        </DockPanel>
        <Button x:Name='QuitBtn' Content='退出桌宠' Margin='0,18,0,0' Height='32' Style='{StaticResource Accent}'/>
      </StackPanel>
    </ScrollViewer>
  </Border>
</Window>";

        private readonly Config cfg;
        private readonly Engine engine;
        private Window root;
        private readonly Dictionary<string, FrameworkElement> ui = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, TextBlock> statusById = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, PasswordBox> keyBoxes = new Dictionary<string, PasswordBox>();
        private bool updating;

        public PetWindow ActivePet;
        public bool Pinned;

        public SettingsPanel(Config config, Engine engineRef)
        {
            cfg = config;
            engine = engineRef;
            Pinned = false;
        }

        public void Build()
        {
            root = (Window)XamlReader.Parse(Xaml);
            foreach (string name in new string[]
            {
                "Header","CloseBtn","SubText","ActionGrid","AutoActionsChk","RolePanel","ScaleSlider","ScaleVal",
                "OpacitySlider","OpacityVal","WalkChk","SnapChk","SleepChk","QuipsChk","SpeedSlider","SpeedVal",
                "ActionPauseSlider","ActionPauseVal","ProviderList","UnsupportedPanel","RefreshBtn","DataText",
                "PlanList",
                "EdgeHideChk","EdgeDelayBox","AlertBelowBox","BudgetBox","AlertChk","BudgetChk",
                "CloneAutoChk","CloneMaxBox","CloneAddBtn","CloneRemoveBtn","CloneCount","QuitBtn"
            })
            {
                ui[name] = (FrameworkElement)root.FindName(name);
            }
            root.Deactivated += delegate { if (!Pinned) HidePanel(); };
            root.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) HidePanel(); };
            ((FrameworkElement)ui["Header"]).MouseLeftButtonDown += delegate { try { root.DragMove(); } catch { } };
            ((Button)ui["CloseBtn"]).Click += delegate { HidePanel(); };

            Button randomButton = new Button();
            randomButton.Content = "随机";
            randomButton.Style = (Style)root.Resources["Accent"];
            randomButton.Click += delegate { engine.PerformAction(ActivePet, "random", true); };
            ((UniformGrid)ui["ActionGrid"]).Children.Add(randomButton);
            foreach (string key in PetWindow.ActionKeys)
            {
                Button button = new Button();
                button.Content = Engine.ActionName(key);
                button.Style = (Style)root.Resources["Btn"];
                string action = key;
                button.Click += delegate { engine.PerformAction(ActivePet, action, true); };
                ((UniformGrid)ui["ActionGrid"]).Children.Add(button);
            }
            BuildRoleButtons();
            BuildProviderRows();
            BuildPlanRows();

            ((Slider)ui["ScaleSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.Set("", "scale", Math.Round(e.NewValue, 3));
                foreach (PetWindow pet in engine.Pets) pet.ResizePet();
                ((TextBlock)ui["ScaleVal"]).Text = e.NewValue.ToString("0.00", CultureInfo.InvariantCulture) + "x";
                cfg.Save();
            };
            ((Slider)ui["OpacitySlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.Set("", "opacity", Math.Round(e.NewValue, 2));
                foreach (PetWindow pet in engine.Pets) pet.Opacity = e.NewValue;
                ((TextBlock)ui["OpacityVal"]).Text = ((int)Math.Round(e.NewValue * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                cfg.Save();
            };
            ((Slider)ui["SpeedSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.Set("walk", "speedPxPerSec", (int)e.NewValue);
                ((TextBlock)ui["SpeedVal"]).Text = ((int)e.NewValue).ToString(CultureInfo.InvariantCulture);
                cfg.Save();
            };
            ((Slider)ui["ActionPauseSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                int min = (int)e.NewValue;
                cfg.Set("actions", "pauseMinSec", min);
                cfg.Set("actions", "pauseMaxSec", (int)(min * 2.6));
                ((TextBlock)ui["ActionPauseVal"]).Text = min.ToString(CultureInfo.InvariantCulture) + "s";
                cfg.Save();
            };
            BindSwitch("AutoActionsChk", "actions", "enabled");
            BindSwitch("WalkChk", "walk", "enabled");
            BindSwitch("SnapChk", "snap", "enabled");
            BindSwitch("SleepChk", "sleep", "enabled");
            BindSwitch("QuipsChk", "alert", "quipsOn");
            BindSwitch("AlertChk", "alert", "balanceWarn");
            BindSwitch("BudgetChk", "alert", "budgetWarn");
            BindSwitch("EdgeHideChk", "edgeHide", "enabled");
            BindSwitch("CloneAutoChk", "clones", "auto");
            BindNumberBox("AlertBelowBox", "alert.below");
            BindNumberBox("BudgetBox", "alert.budget");
            BindNumberBox("EdgeDelayBox", "edgeHide.delaySec");
            BindNumberBox("CloneMaxBox", "clones.max");
            ((Button)ui["RefreshBtn"]).Click += delegate { engine.Data.Refresh(); };
            ((Button)ui["CloneAddBtn"]).Click += delegate { engine.AddClone(); Update(); };
            ((Button)ui["CloneRemoveBtn"]).Click += delegate { engine.RemoveClone(); Update(); };
            ((Button)ui["QuitBtn"]).Click += delegate { engine.Quit(); };
        }

        private void BindSwitch(string name, string section, string key)
        {
            CheckBox box = (CheckBox)ui[name];
            box.Click += delegate
            {
                if (updating) return;
                cfg.Set(section, key, box.IsChecked == true);
                if (section == "snap" && box.IsChecked == true)
                {
                    foreach (PetWindow pet in engine.Pets) pet.Snap();
                }
                cfg.Save();
            };
        }

        private void BindNumberBox(string name, string path)
        {
            TextBox box = (TextBox)ui[name];
            box.LostFocus += delegate { SaveNumberBox(box, path); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) SaveNumberBox(box, path);
            };
        }

        private void SaveNumberBox(TextBox box, string path)
        {
            double value;
            if (!double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0) value = 0;
            cfg.SetAt(path, value);
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        // ------------------------------------------------------------ 角色
        private void BuildRoleButtons()
        {
            WrapPanel panel = (WrapPanel)ui["RolePanel"];
            string selected = cfg.GetString("", "role", "");
            AddRoleButton(panel, "", "内置小鲸鱼", selected, null);
            List<string> files = AppPaths.RoleFiles();
            foreach (string file in files)
            {
                AddRoleButton(panel, file, Path.GetFileNameWithoutExtension(file), selected, file);
            }
            if (files.Count == 0)
            {
                TextBlock hint = new TextBlock();
                hint.Text = "把角色 PNG 放进 roles\\ 目录即可在这里选用";
                hint.FontSize = 10;
                hint.Foreground = Brush("#8FA0C8");
                hint.TextWrapping = TextWrapping.Wrap;
                hint.Margin = new Thickness(2, 6, 0, 0);
                panel.Children.Add(hint);
            }
        }

        private void AddRoleButton(WrapPanel panel, string tag, string label, string selected, string thumbFile)
        {
            Button button = new Button();
            button.Style = (Style)root.Resources["Btn"];
            button.Margin = new Thickness(0, 0, 7, 7);
            button.Tag = tag;
            StackPanel stack = new StackPanel();
            try
            {
                FrameworkElement thumb;
                if (string.IsNullOrEmpty(thumbFile))
                {
                    thumb = Mascot.CreateThumbnail(42, 36);
                }
                else
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(Path.Combine(AppPaths.RolesDir, thumbFile));
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 68;
                    bitmap.EndInit();
                    Image image = new Image();
                    image.Source = bitmap;
                    image.Width = 42;
                    image.Height = 46;
                    image.Stretch = Stretch.Uniform;
                    thumb = image;
                }
                stack.Children.Add(thumb);
            }
            catch
            {
            }
            TextBlock text = new TextBlock();
            text.Text = label;
            text.FontSize = 9;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(text);
            button.Content = stack;
            string chosen = tag;
            button.Click += delegate
            {
                cfg.Set("", "role", chosen);
                foreach (PetWindow pet in engine.Pets) pet.ApplyRole(chosen);
                cfg.Save();
                Update();
            };
            button.BorderBrush = Convert.ToString(button.Tag) == selected ? Brush("#4B6BFB") : Brushes.Transparent;
            button.BorderThickness = new Thickness(Convert.ToString(button.Tag) == selected ? 2 : 0);
            panel.Children.Add(button);
        }

        // ------------------------------------------------------------ 数据源
        private void BuildProviderRows()
        {
            StackPanel list = (StackPanel)ui["ProviderList"];
            WrapPanel unsupported = (WrapPanel)ui["UnsupportedPanel"];
            list.Children.Clear();
            unsupported.Children.Clear();
            foreach (ProviderDef def in Providers.Effective(cfg))
            {
                if (!def.HasBalance)
                {
                    Border chip = new Border();
                    chip.CornerRadius = new CornerRadius(8);
                    chip.Background = Brush("#202A42");
                    chip.Padding = new Thickness(8, 3, 8, 3);
                    chip.Margin = new Thickness(0, 0, 6, 6);
                    chip.ToolTip = def.Note;
                    TextBlock chipText = new TextBlock();
                    chipText.Text = def.Name;
                    chipText.FontSize = 10.5;
                    chipText.Foreground = Brush("#8FA0C8");
                    chip.Child = chipText;
                    unsupported.Children.Add(chip);
                    continue;
                }
                list.Children.Add(BuildProviderRow(def));
            }
        }

        private FrameworkElement BuildProviderRow(ProviderDef def)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Background = Brush("#1B2236");
            row.Padding = new Thickness(10, 7, 10, 9);
            row.Margin = new Thickness(0, 0, 0, 7);
            StackPanel stack = new StackPanel();

            DockPanel head = new DockPanel();
            TextBlock status = new TextBlock();
            status.FontSize = 10.5;
            status.Foreground = Brush("#8FA0C8");
            status.VerticalAlignment = VerticalAlignment.Center;
            status.Margin = new Thickness(8, 0, 0, 0);
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            status.MaxWidth = 210;
            DockPanel.SetDock(status, Dock.Right);
            head.Children.Add(status);
            CheckBox toggle = new CheckBox();
            toggle.Style = (Style)root.Resources["Switch"];
            toggle.Content = def.Name;
            toggle.Margin = new Thickness(0);
            toggle.IsChecked = cfg.GetFlagAt("providers." + def.Id + ".on", def.Id == "deepseek");
            string id = def.Id;
            toggle.Click += delegate
            {
                cfg.SetAt("providers." + id + ".on", toggle.IsChecked == true);
                cfg.Save();
                engine.Data.Refresh();
                Update();
            };
            head.Children.Add(toggle);
            stack.Children.Add(head);
            statusById[def.Id] = status;

            if (def.IsCustom)
            {
                stack.Children.Add(FieldRow("名称", "custom.name", 150, 120));
                stack.Children.Add(FieldRow("URL", "custom.url", 0, 44));
                stack.Children.Add(FieldRow("路径", "custom.path", 0, 44));
                stack.Children.Add(FieldRow("币种", "custom.currency", 60, 44));
                DockPanel authRow = new DockPanel();
                authRow.Margin = new Thickness(0, 6, 0, 0);
                Button auth = new Button();
                auth.Style = (Style)root.Resources["Mini"];
                auth.Padding = new Thickness(8, 0, 8, 0);
                auth.Content = AuthLabel(cfg.GetString("custom", "auth", "bearer"));
                auth.Click += delegate
                {
                    string next = NextAuth(cfg.GetString("custom", "auth", "bearer"));
                    cfg.Set("custom", "auth", next);
                    auth.Content = AuthLabel(next);
                    cfg.Save();
                    engine.Data.Refresh();
                };
                DockPanel.SetDock(auth, Dock.Right);
                authRow.Children.Add(auth);
                TextBlock authHint = new TextBlock();
                authHint.Text = "URL 里可用 {key} 占位；路径留空表示响应体本身就是数字";
                authHint.FontSize = 10;
                authHint.Foreground = Brush("#8FA0C8");
                authHint.TextWrapping = TextWrapping.Wrap;
                authHint.VerticalAlignment = VerticalAlignment.Center;
                authRow.Children.Add(authHint);
                stack.Children.Add(authRow);
            }
            else
            {
                TextBlock note = new TextBlock();
                note.Text = def.Note;
                note.FontSize = 10;
                note.Foreground = Brush("#7d8bb0");
                note.TextWrapping = TextWrapping.Wrap;
                note.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(note);
            }

            DockPanel keyRow = new DockPanel();
            keyRow.Margin = new Thickness(0, 7, 0, 0);
            Button apply = new Button();
            apply.Style = (Style)root.Resources["Mini"];
            apply.Content = "应用";
            apply.Width = 46;
            apply.Padding = new Thickness(0);
            DockPanel.SetDock(apply, Dock.Right);
            apply.Click += delegate { ApplyKey(id); };
            Button clear = new Button();
            clear.Style = (Style)root.Resources["Mini"];
            clear.Content = "清除";
            clear.Width = 46;
            clear.Padding = new Thickness(0);
            clear.Margin = new Thickness(0, 0, 6, 0);
            DockPanel.SetDock(clear, Dock.Right);
            clear.Click += delegate { ClearKey(id); };
            keyRow.Children.Add(apply);
            keyRow.Children.Add(clear);
            PasswordBox box = new PasswordBox();
            box.Style = (Style)root.Resources["KeyBox"];
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) ApplyKey(id);
            };
            keyBoxes[def.Id] = box;
            keyRow.Children.Add(box);
            stack.Children.Add(keyRow);

            row.Child = stack;
            return row;
        }

        private FrameworkElement FieldRow(string label, string path, double boxWidth, double labelWidth)
        {
            DockPanel row = new DockPanel();
            row.Margin = new Thickness(0, 6, 0, 0);
            TextBlock prefix = new TextBlock();
            prefix.Text = label;
            prefix.FontSize = 11;
            prefix.Foreground = Brush("#8FA0C8");
            prefix.Width = labelWidth;
            prefix.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(prefix, Dock.Left);
            row.Children.Add(prefix);
            TextBox box = new TextBox();
            box.Style = (Style)root.Resources["TxtBox"];
            box.Text = cfg.GetStringAt(path, "");
            if (boxWidth > 0) box.Width = boxWidth;
            box.LostFocus += delegate { SaveField(path, box.Text); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) SaveField(path, box.Text);
            };
            row.Children.Add(box);
            return row;
        }

        private void SaveField(string path, string value)
        {
            cfg.SetAt(path, value.Trim());
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        private void ApplyKey(string id)
        {
            PasswordBox box = keyBoxes[id];
            string text = box.Password.Trim();
            if (text.Length == 0) return;
            cfg.SetAt("providers." + id + ".apiKey", Secret.Protect(text));
            box.Password = "";
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        private void ClearKey(string id)
        {
            cfg.SetAt("providers." + id + ".apiKey", "");
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        private static string AuthLabel(string auth)
        {
            if (auth == "x-api-key") return "认证：x-api-key";
            if (auth == "none") return "认证：无";
            return "认证：Bearer";
        }

        private static string NextAuth(string auth)
        {
            if (auth == "bearer") return "x-api-key";
            if (auth == "x-api-key") return "none";
            return "bearer";
        }

        private void UpdateProviderStatus()
        {
            Snapshot data = engine.Data.Current;
            foreach (KeyValuePair<string, TextBlock> pair in statusById)
            {
                string id = pair.Key;
                ProviderState state = null;
                foreach (ProviderState item in data.Providers)
                {
                    if (item.Id == id)
                    {
                        state = item;
                        break;
                    }
                }
                bool on = cfg.GetFlagAt("providers." + id + ".on", id == "deepseek");
                bool hasKey = cfg.GetStringAt("providers." + id + ".apiKey", "") != "";
                string text;
                string colour;
                if (!on)
                {
                    text = "已关闭";
                    colour = "#8FA0C8";
                }
                else if (state == null)
                {
                    text = "等待查询";
                    colour = "#8FA0C8";
                }
                else if (state.Ok)
                {
                    string suffix = string.IsNullOrEmpty(state.KeySource) || state.KeySource == "pet-config.json" ? "" : " · " + state.KeySource;
                    text = Fmt.Money(state.Balance, state.Currency) + suffix;
                    colour = "#6FD08C";
                }
                else
                {
                    text = state.Error;
                    colour = "#FF8B7A";
                }
                if (text != null && text.Length > 26) text = text.Substring(0, 26) + "…";
                pair.Value.Text = text;
                pair.Value.Foreground = Brush(colour);
                pair.Value.ToolTip = state != null && !string.IsNullOrEmpty(state.ErrorDetail) ? state.ErrorDetail : null;
            }
        }

        // ------------------------------------------------------------ 套餐用量
        private readonly Dictionary<string, TextBlock> planStatus = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, PasswordBox> planBoxes = new Dictionary<string, PasswordBox>();

        private void BuildPlanRows()
        {
            StackPanel list = (StackPanel)ui["PlanList"];
            list.Children.Clear();
            foreach (PlanDef def in Plans.Catalog())
            {
                Border row = new Border();
                row.CornerRadius = new CornerRadius(9);
                row.Background = Brush("#1B2236");
                row.Padding = new Thickness(10, 7, 10, 9);
                row.Margin = new Thickness(0, 0, 0, 7);
                StackPanel stack = new StackPanel();

                DockPanel head = new DockPanel();
                TextBlock status = new TextBlock();
                status.FontSize = 10.5;
                status.Foreground = Brush("#8FA0C8");
                status.VerticalAlignment = VerticalAlignment.Center;
                status.Margin = new Thickness(8, 0, 0, 0);
                status.TextTrimming = TextTrimming.CharacterEllipsis;
                status.MaxWidth = 210;
                DockPanel.SetDock(status, Dock.Right);
                head.Children.Add(status);
                CheckBox toggle = new CheckBox();
                toggle.Style = (Style)root.Resources["Switch"];
                toggle.Content = def.Name;
                toggle.Margin = new Thickness(0);
                toggle.IsChecked = cfg.GetFlagAt("plans." + def.Id + ".on", false);
                string id = def.Id;
                toggle.Click += delegate
                {
                    cfg.SetAt("plans." + id + ".on", toggle.IsChecked == true);
                    cfg.Save();
                    engine.Data.Refresh();
                    Update();
                };
                head.Children.Add(toggle);
                stack.Children.Add(head);
                planStatus[def.Id] = status;

                TextBlock note = new TextBlock();
                note.Text = def.Note;
                note.FontSize = 10;
                note.Foreground = Brush("#7d8bb0");
                note.TextWrapping = TextWrapping.Wrap;
                note.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(note);

                if (def.IsCustom)
                {
                    stack.Children.Add(FieldRow("URL", "plans.custom.url", 0, 44));
                    stack.Children.Add(FieldRow("路径", "plans.custom.path", 0, 44));
                }

                DockPanel keyRow = new DockPanel();
                keyRow.Margin = new Thickness(0, 7, 0, 0);
                Button apply = new Button();
                apply.Style = (Style)root.Resources["Mini"];
                apply.Content = "应用";
                apply.Width = 46;
                apply.Padding = new Thickness(0);
                DockPanel.SetDock(apply, Dock.Right);
                apply.Click += delegate { ApplyPlanKey(id); };
                Button clear = new Button();
                clear.Style = (Style)root.Resources["Mini"];
                clear.Content = "清除";
                clear.Width = 46;
                clear.Padding = new Thickness(0);
                clear.Margin = new Thickness(0, 0, 6, 0);
                DockPanel.SetDock(clear, Dock.Right);
                clear.Click += delegate { ClearPlanKey(id); };
                keyRow.Children.Add(apply);
                keyRow.Children.Add(clear);
                PasswordBox box = new PasswordBox();
                box.Style = (Style)root.Resources["KeyBox"];
                box.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.Key == Key.Enter) ApplyPlanKey(id);
                };
                planBoxes[def.Id] = box;
                keyRow.Children.Add(box);
                stack.Children.Add(keyRow);

                row.Child = stack;
                list.Children.Add(row);
            }
        }

        private void ApplyPlanKey(string id)
        {
            PasswordBox box = planBoxes[id];
            string text = box.Password.Trim();
            if (text.Length == 0) return;
            cfg.SetAt("plans." + id + ".apiKey", Secret.Protect(text));
            box.Password = "";
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        private void ClearPlanKey(string id)
        {
            cfg.SetAt("plans." + id + ".apiKey", "");
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        private void UpdatePlanStatus()
        {
            Snapshot data = engine.Data.Current;
            foreach (KeyValuePair<string, TextBlock> pair in planStatus)
            {
                string id = pair.Key;
                PlanState state = null;
                foreach (PlanState item in data.Plans)
                {
                    if (item.Id == id)
                    {
                        state = item;
                        break;
                    }
                }
                bool on = cfg.GetFlagAt("plans." + id + ".on", false);
                string text;
                string colour;
                if (!on)
                {
                    text = "已关闭";
                    colour = "#8FA0C8";
                }
                else if (state == null)
                {
                    text = "等待查询";
                    colour = "#8FA0C8";
                }
                else if (state.Ok)
                {
                    text = state.Summary();
                    if (!string.IsNullOrEmpty(state.KeySource)) text += " · " + state.KeySource;
                    colour = "#6FD08C";
                }
                else
                {
                    text = state.Error;
                    colour = "#FF8B7A";
                }
                if (text != null && text.Length > 30) text = text.Substring(0, 30) + "…";
                pair.Value.Text = text;
                pair.Value.Foreground = Brush(colour);
                pair.Value.ToolTip = state != null && !string.IsNullOrEmpty(state.ErrorDetail) ? state.ErrorDetail : null;
            }
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        // ------------------------------------------------------------ 显示 / 刷新
        public void ShowFor(PetWindow pet)
        {
            ActivePet = pet;
            Update();
            root.Visibility = Visibility.Visible;
            if (pet != null)
            {
                // Rough placement first, then refine once the window has measured itself.
                root.Left = Math.Round(pet.X + pet.Width + 12, 1);
                root.Top = Math.Round(pet.Y, 1);
            }
            root.Show();
            root.UpdateLayout();
            if (pet != null)
            {
                ScreenArea area = ScreenArea.ForPoint(pet.X + pet.Width / 2, pet.Y + pet.Height / 2);
                double x = pet.X + pet.Width + 12;
                if (x + root.ActualWidth > area.Right - 4) x = pet.X - root.ActualWidth - 12;
                x = Math.Max(area.Left + 4, Math.Min(x, area.Right - root.ActualWidth - 4));
                double y = Math.Max(area.Top + 4, Math.Min(pet.Y, area.Bottom - root.ActualHeight - 4));
                root.Left = Math.Round(x, 1);
                root.Top = Math.Round(y, 1);
            }
            root.Activate();
        }

        public void HidePanel()
        {
            if (root != null) root.Visibility = Visibility.Hidden;
        }

        public new bool IsVisible
        {
            get { return root != null && root.Visibility == Visibility.Visible; }
        }

        public void Update()
        {
            if (root == null) return;
            updating = true;
            try
            {
                ((Slider)ui["ScaleSlider"]).Value = cfg.GetNumber("", "scale", 1.0);
                ((TextBlock)ui["ScaleVal"]).Text = cfg.GetNumber("", "scale", 1.0).ToString("0.00", CultureInfo.InvariantCulture) + "x";
                ((Slider)ui["OpacitySlider"]).Value = cfg.GetNumber("", "opacity", 1.0);
                ((TextBlock)ui["OpacityVal"]).Text = ((int)Math.Round(cfg.GetNumber("", "opacity", 1.0) * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                ((Slider)ui["SpeedSlider"]).Value = cfg.GetNumber("walk", "speedPxPerSec", 72);
                ((TextBlock)ui["SpeedVal"]).Text = ((int)cfg.GetNumber("walk", "speedPxPerSec", 72)).ToString(CultureInfo.InvariantCulture);
                ((Slider)ui["ActionPauseSlider"]).Value = cfg.GetNumber("actions", "pauseMinSec", 14);
                ((TextBlock)ui["ActionPauseVal"]).Text = ((int)cfg.GetNumber("actions", "pauseMinSec", 14)).ToString(CultureInfo.InvariantCulture) + "s";
                ((CheckBox)ui["AutoActionsChk"]).IsChecked = cfg.GetFlag("actions", "enabled", true);
                ((CheckBox)ui["WalkChk"]).IsChecked = cfg.GetFlag("walk", "enabled", true);
                ((CheckBox)ui["SnapChk"]).IsChecked = cfg.GetFlag("snap", "enabled", true);
                ((CheckBox)ui["SleepChk"]).IsChecked = cfg.GetFlag("sleep", "enabled", true);
                ((CheckBox)ui["QuipsChk"]).IsChecked = cfg.GetFlag("alert", "quipsOn", true);
                ((CheckBox)ui["AlertChk"]).IsChecked = cfg.GetFlag("alert", "balanceWarn", true);
                ((CheckBox)ui["BudgetChk"]).IsChecked = cfg.GetFlag("alert", "budgetWarn", true);
                ((CheckBox)ui["EdgeHideChk"]).IsChecked = cfg.GetFlag("edgeHide", "enabled", true);
                ((CheckBox)ui["CloneAutoChk"]).IsChecked = cfg.GetFlagAt("clones.auto", true);
                double below = cfg.GetNumberAt("alert.below", 0);
                ((TextBox)ui["AlertBelowBox"]).Text = below > 0 ? below.ToString("0.##", CultureInfo.InvariantCulture) : "";
                double budgetValue = cfg.GetNumberAt("alert.budget", 0);
                ((TextBox)ui["BudgetBox"]).Text = budgetValue > 0 ? budgetValue.ToString("0.##", CultureInfo.InvariantCulture) : "";
                ((TextBox)ui["EdgeDelayBox"]).Text = cfg.GetNumberAt("edgeHide.delaySec", 10).ToString("0.#", CultureInfo.InvariantCulture);
                ((TextBox)ui["CloneMaxBox"]).Text = ((int)engine.CloneCap()).ToString(CultureInfo.InvariantCulture);
                ((TextBlock)ui["CloneCount"]).Text = engine.Pets.Count.ToString(CultureInfo.InvariantCulture) + " 只";
                string role = cfg.GetString("", "role", "");
                foreach (object child in ((WrapPanel)ui["RolePanel"]).Children)
                {
                    Button button = child as Button;
                    if (button == null) continue;
                    bool selected = Convert.ToString(button.Tag) == role;
                    button.BorderBrush = selected ? Brush("#4B6BFB") : Brushes.Transparent;
                    button.BorderThickness = new Thickness(selected ? 2 : 0);
                }
                UpdateDataText();
            }
            finally
            {
                updating = false;
            }
        }

        public void UpdateDataText()
        {
            if (root == null) return;
            Snapshot data = engine.Data.Current;
            ProviderState primary = data.Primary;
            ((TextBlock)ui["DataText"]).Text = primary == null
                ? "无数据源"
                : primary.Name + " · " + Fmt.Money(primary.Balance, primary.Currency) + " · " + engine.PeakText();
            ((TextBlock)ui["SubText"]).Text = engine.PeakLine() + "　" + data.OkCount + " 个数据源在线";
            UpdateProviderStatus();
            UpdatePlanStatus();
        }
    }
}
