// Modern settings card (dark, rounded, custom switches/sliders) built from an inline
// XAML template with single-quoted attributes so the source stays readable in C# 5.
// Provider rows and key inputs are generated in code because the provider list is data.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rectangle = System.Windows.Shapes.Rectangle;
using Ellipse = System.Windows.Shapes.Ellipse;
using Path = System.Windows.Shapes.Path;

namespace BalancePet
{
    internal sealed class SettingsPanel : Window
    {
        private const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        WindowStyle='None' AllowsTransparency='True' Background='{x:Null}' Topmost='True'
        ShowInTaskbar='False' SizeToContent='Manual' Width='680' Height='748' ResizeMode='NoResize'
        FontFamily='Microsoft YaHei UI, Segoe UI' TextOptions.TextFormattingMode='Display'>
  <Window.Resources>
    <Style x:Key='Btn' TargetType='Button'>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='Margin' Value='0,0,7,7'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='BorderBrush' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='__BTN__' CornerRadius='9' Padding='11,0'
                    BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
              <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                                VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='__BTN_HOVER__'/></Trigger>
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
    <Style x:Key='Nav' TargetType='Button'>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='Height' Value='38'/>
      <Setter Property='Margin' Value='0,0,0,4'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='navBg' Background='{TemplateBinding Background}' CornerRadius='10'
                    BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'
                    Padding='8,0'>
              <ContentPresenter VerticalAlignment='Center' HorizontalAlignment='Stretch'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='navBg' Property='Background' Value='__BTN_HOVER__'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
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
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Margin' Value='0,5,16,5'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='CheckBox'>
            <StackPanel Orientation='Horizontal' Background='Transparent'>
              <Border x:Name='track' Width='34' Height='19' CornerRadius='10' Background='__TRACK__'>
                <Ellipse x:Name='knob' Width='13' Height='13' Fill='__MUTED__' HorizontalAlignment='Left' Margin='3,0,0,0'/>
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
      <Setter Property='Height' Value='28'/>
      <Setter Property='SnapsToDevicePixels' Value='True'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Slider'>
            <Grid Background='Transparent'>
              <Border Height='5' CornerRadius='3' Background='__TRACK__' VerticalAlignment='Center'/>
              <Track x:Name='PART_Track'>
                <Track.DecreaseRepeatButton>
                  <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>
                    <RepeatButton.Template>
                      <ControlTemplate TargetType='RepeatButton'>
                        <Border Height='5' CornerRadius='3' Background='#4B6BFB' VerticalAlignment='Center'/>
                      </ControlTemplate>
                    </RepeatButton.Template>
                  </RepeatButton>
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                  <Thumb Width='16' Height='16' Focusable='False' Cursor='Hand'>
                    <Thumb.Template>
                      <ControlTemplate TargetType='Thumb'>
                        <Grid Background='Transparent'>
                          <Ellipse x:Name='knob' Fill='#FFFFFF' Stroke='#4B6BFB' StrokeThickness='3'/>
                        </Grid>
                        <ControlTemplate.Triggers>
                          <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='knob' Property='StrokeThickness' Value='4'/>
                          </Trigger>
                          <Trigger Property='IsDragging' Value='True'>
                            <Setter TargetName='knob' Property='StrokeThickness' Value='4'/>
                          </Trigger>
                        </ControlTemplate.Triggers>
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
            <ControlTemplate.Triggers>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Opacity' Value='0.42'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Sect' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Foreground' Value='__MUTED__'/>
      <Setter Property='Margin' Value='1,16,0,7'/>
    </Style>
    <Style x:Key='Val' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='10.5'/>
      <Setter Property='FontFamily' Value='Consolas, Microsoft YaHei UI'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='MinWidth' Value='52'/>
      <Setter Property='TextAlignment' Value='Right'/>
      <Setter Property='Margin' Value='8,0,0,0'/>
    </Style>
    <Style x:Key='Lbl' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style x:Key='SliderTitle' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='10.5'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Foreground' Value='__MUTED__'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style x:Key='SliderValue' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='10'/>
      <Setter Property='FontFamily' Value='Consolas, Microsoft YaHei UI'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='HorizontalAlignment' Value='Right'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='MinWidth' Value='44'/>
      <Setter Property='TextAlignment' Value='Right'/>
    </Style>
    <Style x:Key='FieldLbl' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='10'/>
      <Setter Property='Foreground' Value='__MUTED__'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='Margin' Value='1,0,7,7'/>
    </Style>
    <Style x:Key='KeyBox' TargetType='PasswordBox'>
      <Setter Property='Height' Value='26'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='CaretBrush' Value='White'/>
      <Setter Property='Background' Value='__FIELD__'/>
      <Setter Property='BorderBrush' Value='__TRACK__'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Padding' Value='6,0'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
    </Style>
    <Style x:Key='TxtBox' TargetType='TextBox'>
      <Setter Property='Height' Value='26'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='CaretBrush' Value='White'/>
      <Setter Property='Background' Value='__FIELD__'/>
      <Setter Property='BorderBrush' Value='__TRACK__'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Padding' Value='6,0'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
    </Style>
  </Window.Resources>
  <Grid Margin='40'>
    <Grid x:Name='PanelShadowLayer' Margin='-40' IsHitTestVisible='False'>
      <Border x:Name='PanelShadowShape' Background='Black' CornerRadius='16' Margin='40'>
        <Border.Effect>
          <DropShadowEffect x:Name='PanelShadow' BlurRadius='26' ShadowDepth='6' Opacity='0.5' Color='#000000'/>
        </Border.Effect>
      </Border>
    </Grid>
    <Border x:Name='PanelCard' Background='__CARD__' CornerRadius='16' BorderBrush='__CARD_BORDER__' BorderThickness='1' Padding='18,13,18,15'>
      <Grid x:Name='ContentRoot' Width='564' Height='640'>
        <Grid.RowDefinitions>
          <RowDefinition Height='Auto'/>
          <RowDefinition Height='6'/>
          <RowDefinition Height='*'/>
        </Grid.RowDefinitions>
        <DockPanel x:Name='Header' Grid.Row='0' Background='Transparent'>
          <Button x:Name='SaveSettingsBtn' DockPanel.Dock='Right' Content='保存设置' Width='86' Height='26' Margin='0,0,8,0' FontSize='11' Style='{StaticResource Accent}'/>
          <TextBlock x:Name='SaveStateText' DockPanel.Dock='Right' Text='已保存' FontSize='10' Foreground='__MUTED__' VerticalAlignment='Center' Margin='0,0,9,0'/>
          <StackPanel Margin='0,2,0,0'>
            <TextBlock x:Name='TitleText' Text='余额桌宠' FontSize='15' FontWeight='Bold' Foreground='__FG__'/>
            <TextBlock x:Name='SubText' Text='…' FontSize='10' Foreground='__MUTED__' Margin='0,3,12,0'
                       TextTrimming='CharacterEllipsis' ToolTip='…'/>
          </StackPanel>
        </DockPanel>
        <Grid Grid.Row='2'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='140'/>
            <ColumnDefinition Width='8'/>
            <ColumnDefinition Width='*'/>
          </Grid.ColumnDefinitions>
          <Border x:Name='NavHost' Grid.Column='0' Background='__NAV__' CornerRadius='10' Padding='6,6,7,6'>
            <Grid>
              <Grid.RowDefinitions>
                <RowDefinition Height='*'/>
                <RowDefinition Height='Auto'/>
              </Grid.RowDefinitions>
              <StackPanel x:Name='Nav' Grid.Row='0'/>
              <Button x:Name='QuitBtn' Grid.Row='1' Content='退出桌宠' Width='120' Height='28' Margin='0,8,0,0'
                      FontSize='11' Style='{StaticResource Btn}' Foreground='#E5484D'
                      BorderBrush='#E5484D' BorderThickness='1' ToolTip='退出桌宠（结束进程）'/>
            </Grid>
          </Border>
          <Grid Grid.Column='2'>
            <Grid.RowDefinitions>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='*'/>
            </Grid.RowDefinitions>
            <Border x:Name='PreviewDock' Grid.Row='0' Height='250' Margin='0,0,8,8'
                    CornerRadius='12' Padding='10' Visibility='Collapsed'
                    Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'
                    IsHitTestVisible='False'>
              <Grid>
                <Grid.RowDefinitions>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='*'/>
                </Grid.RowDefinitions>
                <TextBlock x:Name='PreviewDockTitle' Text='实时预览' FontSize='10'
                           FontWeight='SemiBold' Foreground='__MUTED__' Margin='1,0,0,6'/>
                <Border Grid.Row='1' CornerRadius='8' Background='__DESK__' ClipToBounds='True'>
                  <Grid>
                    <ContentControl x:Name='PreviewVisualHost' Margin='8'
                                    HorizontalContentAlignment='Center' VerticalContentAlignment='Center'/>
                    <ContentControl x:Name='PreviewSummaryHost' Width='300' Margin='10'
                                    HorizontalAlignment='Center' VerticalAlignment='Center'
                                    HorizontalContentAlignment='Stretch' VerticalContentAlignment='Center'/>
                  </Grid>
                </Border>
              </Grid>
            </Border>
          <ScrollViewer x:Name='PageScroll' Grid.Row='1' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
            <ScrollViewer.Resources>
              <Style TargetType='ScrollBar'>
                <Setter Property='Width' Value='10'/>
                <Setter Property='Background' Value='Transparent'/>
                <Setter Property='Margin' Value='2,2,1,2'/>
                <Setter Property='Template'>
                  <Setter.Value>
                    <ControlTemplate TargetType='ScrollBar'>
                      <Grid Background='Transparent' Width='8'>
                        <Track x:Name='PART_Track' IsDirectionReversed='True'>
                          <Track.DecreaseRepeatButton>
                            <RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/>
                          </Track.DecreaseRepeatButton>
                          <Track.Thumb>
                            <Thumb>
                              <Thumb.Template>
                                <ControlTemplate TargetType='Thumb'>
                                  <Border x:Name='t' Background='__BORDER__' CornerRadius='4' MinHeight='36' Margin='2,0' Opacity='0.85'/>
                                  <ControlTemplate.Triggers>
                                    <Trigger Property='IsMouseOver' Value='True'>
                                      <Setter TargetName='t' Property='Opacity' Value='1'/>
                                    </Trigger>
                                  </ControlTemplate.Triggers>
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
            <StackPanel x:Name='Main' Margin='0,3,8,12'/>
          </ScrollViewer>
          </Grid>
        </Grid>
        <StackPanel x:Name='PageSource' Visibility='Collapsed'>
        <TextBlock Style='{StaticResource Sect}' Text='主题与气泡'/>
        <TextBlock Text='界面' FontSize='10' FontWeight='SemiBold' Foreground='__MUTED__' Margin='1,2,0,5'/>
        <Grid Margin='0,0,0,4'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='58'/><ColumnDefinition Width='*'/>
            <ColumnDefinition Width='58'/><ColumnDefinition Width='*'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource FieldLbl}' Text='界面主题'/>
          <Button x:Name='UiThemeDropBtn' Grid.Column='1' Content='跟随系统 ▾' Style='{StaticResource Btn}'/>
          <TextBlock Style='{StaticResource FieldLbl}' Text='气泡主题' Grid.Column='2'/>
          <Button x:Name='ThemeDropBtn' Grid.Column='3' Content='浅色 ▾' Style='{StaticResource Btn}'/>
        </Grid>
        <Grid Margin='0,2,0,4'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='58'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource FieldLbl}' Text='城市/区县'/>
          <TextBox x:Name='WeatherCityBox' Grid.Column='1' Style='{StaticResource TxtBox}' Margin='0,0,8,0'
                   ToolTip='支持“上海 浦东新区”“深圳 南山区”“北京 朝阳区”等城市或区县名称'/>
          <Button x:Name='WeatherLocateBtn' AutomationProperties.AutomationId='WeatherLocateBtn' Grid.Column='2' Content='定位' Width='64' Style='{StaticResource Mini}'
                  Margin='0,0,10,0' ToolTip='通过当前网络位置自动填充城市和区县'/>
          <CheckBox x:Name='WeatherChk' Grid.Column='3' Style='{StaticResource Switch}' Content='顶部天气'
                    VerticalAlignment='Center'/>
        </Grid>
        <TextBlock Text='气泡外观' FontSize='10' FontWeight='SemiBold' Foreground='__MUTED__' Margin='1,4,0,5'/>
        <Grid Margin='0,0,0,4'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='58'/><ColumnDefinition Width='*'/>
            <ColumnDefinition Width='58'/><ColumnDefinition Width='*'/>
          </Grid.ColumnDefinitions>
          <Grid.RowDefinitions>
            <RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/>
          </Grid.RowDefinitions>
          <TextBlock Style='{StaticResource FieldLbl}' Text='风格'/>
          <Button x:Name='BubblePresetBtn' Grid.Column='1' Content='柔和卡片 ▾' Height='64'
                  Padding='8,5' HorizontalContentAlignment='Stretch' Style='{StaticResource Btn}'/>
          <TextBlock Style='{StaticResource FieldLbl}' Text='布局' Grid.Column='2'/>
          <Button x:Name='BubbleLayoutBtn' Grid.Column='3' Content='原版 ▾' Height='64'
                  Padding='8,5' HorizontalContentAlignment='Stretch' Style='{StaticResource Btn}'/>
          <TextBlock Style='{StaticResource FieldLbl}' Text='动效' Grid.Row='1'/>
          <Button x:Name='EffectDropBtn' Grid.Row='1' Grid.Column='1' Content='关闭 ▾' Style='{StaticResource Btn}'/>
          <TextBlock Style='{StaticResource FieldLbl}' Text='字体' Grid.Row='1' Grid.Column='2'/>
          <Button x:Name='FontDropBtn' Grid.Row='1' Grid.Column='3' Content='跟随系统 ▾' Style='{StaticResource Btn}'/>
          <TextBlock Style='{StaticResource FieldLbl}' Text='数字' Grid.Row='2'/>
          <Button x:Name='NumberFontDropBtn' Grid.Row='2' Grid.Column='1' Content='跟随字体 ▾' Style='{StaticResource Btn}'/>
          <TextBlock Style='{StaticResource FieldLbl}' Text='统计' Grid.Row='2' Grid.Column='2'/>
          <Button x:Name='TrendRangeBtn' Grid.Row='2' Grid.Column='3' Content='近 7 天 ▾' Style='{StaticResource Btn}'/>
        </Grid>
        <StackPanel Margin='0,5,0,2'>
          <TextBlock Text='控件颜色' FontSize='10' FontWeight='SemiBold' Foreground='__MUTED__' Margin='1,0,0,4'/>
          <StackPanel Orientation='Horizontal' Margin='1,0,0,0'>
            <TextBlock Text='主色' FontSize='11' Foreground='__MUTED__' VerticalAlignment='Center' Margin='0,0,6,0'/>
            <WrapPanel x:Name='Color1Panel'/>
            <Button x:Name='PickThemeBtn' Content='选择控件色…' Width='100' Style='{StaticResource Btn}'/>
          </StackPanel>
          <StackPanel Orientation='Horizontal' Margin='1,4,0,0'>
            <TextBlock Text='混合色' FontSize='11' Foreground='__MUTED__' VerticalAlignment='Center' Margin='0,0,6,0'/>
            <WrapPanel x:Name='MixPanel'/>
            <Button x:Name='MixAddBtn' Content='添加混合色…' Width='100' Style='{StaticResource Btn}'/>
          </StackPanel>
        </StackPanel>
        <TextBlock Text='显示内容' FontSize='10' FontWeight='SemiBold' Foreground='__MUTED__' Margin='1,3,0,3'/>
        <WrapPanel>
          <CheckBox x:Name='BubbleTrendChk' Style='{StaticResource Switch}' Content='趋势图表'/>
          <CheckBox x:Name='BubbleTailChk' Style='{StaticResource Switch}' Content='气泡尾巴'/>
          <CheckBox x:Name='BubbleReverseChk' Style='{StaticResource Switch}' Content='动效反向'
                    ToolTip='只反转流动、旋转、闪动和漂移的方向；不改变配色深浅'/>
        </WrapPanel>
        <TextBlock Text='“动效反向”只改变动效方向，不改变界面或气泡的深浅主题。'
                   FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='2,2,0,0'/>
        <Border x:Name='FxHost' CornerRadius='12' Padding='12' Margin='0,8,0,0' Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'>
          <StackPanel>
            <DockPanel>
              <TextBlock Text='实时预览' FontSize='10' Foreground='__MUTED__' DockPanel.Dock='Left'/>
              <TextBlock x:Name='FxHint' FontSize='10' Foreground='__MUTED__' HorizontalAlignment='Right'/>
            </DockPanel>
            <Border x:Name='BubblePreviewCanvasHost' Margin='0,8,0,0' Height='150' CornerRadius='8' Background='__DESK__'>
            <Grid>
              <Canvas x:Name='FxCanvas' Height='150' ClipToBounds='False'>
                <Border x:Name='FxCard' CornerRadius='12' Background='__CARD__' BorderBrush='__BORDER__' BorderThickness='1' Padding='10,8'>
                  <StackPanel>
                    <StackPanel Orientation='Horizontal'>
                      <Border x:Name='FxIconTile' Width='21' Height='21' CornerRadius='7' Margin='0,0,7,0'
                              BorderThickness='1'>
                        <Ellipse x:Name='FxDot' Width='6' Height='6' VerticalAlignment='Center' HorizontalAlignment='Center'/>
                      </Border>
                      <TextBlock x:Name='FxTitle' FontSize='11' FontWeight='SemiBold' Text='余额'/>
                    </StackPanel>
                    <TextBlock x:Name='FxAmount' FontSize='19' FontWeight='Bold' Text='¥ 000.00' Margin='0,3,0,0'/>
                    <TextBlock x:Name='FxRow' FontSize='10' Text='今日已用 ¥ 00.00' Margin='0,2,0,0'/>
                    <Grid Width='112' Height='3' Margin='0,6,0,0' HorizontalAlignment='Left'>
                      <Border x:Name='FxMeterTrack' Height='3' CornerRadius='2'/>
                      <Border x:Name='FxMeterFill' Height='3' Width='72' CornerRadius='2' HorizontalAlignment='Left'/>
                    </Grid>
                  </StackPanel>
                </Border>
                <Border x:Name='FxTail' Width='11' Height='11' RenderTransformOrigin='0.5,0.5'>
                  <Border.RenderTransform>
                    <RotateTransform Angle='45'/>
                  </Border.RenderTransform>
                </Border>
              </Canvas>
            </Grid>
            </Border>
          </StackPanel>
        </Border>
        <WrapPanel Margin='0,8,0,0'>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='速率' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleSpeedVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleSpeedSlider' Width='156' Minimum='0.2' Maximum='3' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='深度' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleDepthVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleDepthSlider' Width='156' Minimum='0' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
        </WrapPanel>
        <WrapPanel Margin='0,2,0,0'>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='字号' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleFontVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleFontSlider' Width='156' Minimum='0.7' Maximum='1.8' Style='{StaticResource Flat}'/>
          </StackPanel>
          <WrapPanel x:Name='BubbleCustomSizePanel' Orientation='Horizontal'>
            <StackPanel Width='156' Margin='0,0,10,6'>
              <DockPanel><TextBlock Text='自定义宽' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleWidthVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
              <Slider x:Name='BubbleWidthSlider' Width='156' Minimum='190' Maximum='420' Style='{StaticResource Flat}'/>
            </StackPanel>
            <StackPanel Width='156' Margin='0,0,10,6'>
              <DockPanel><TextBlock Text='自定义高' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleHeightVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
              <Slider x:Name='BubbleHeightSlider' Width='156' Minimum='112' Maximum='280' Style='{StaticResource Flat}'/>
            </StackPanel>
            <StackPanel Width='322' Margin='0,0,10,6'>
              <TextBlock Text='组合模块' FontSize='10' Foreground='__MUTED__' Margin='0,0,0,2'/>
              <WrapPanel>
                <CheckBox x:Name='CustomSplitChk' Style='{StaticResource Switch}' Content='左右分栏'/>
                <CheckBox x:Name='CustomHeaderChk' Style='{StaticResource Switch}' Content='顶部标题'/>
                <CheckBox x:Name='CustomIconChk' Style='{StaticResource Switch}' Content='页面图标'/>
                <CheckBox x:Name='CustomHeroChk' Style='{StaticResource Switch}' Content='主数值'/>
                <CheckBox x:Name='CustomDetailChk' Style='{StaticResource Switch}' Content='明细说明'/>
                <CheckBox x:Name='CustomMetricsChk' Style='{StaticResource Switch}' Content='对比指标'/>
                <CheckBox x:Name='CustomChartChk' Style='{StaticResource Switch}' Content='趋势图'/>
                <CheckBox x:Name='CustomQuipChk' Style='{StaticResource Switch}' Content='随机台词'/>
                <CheckBox x:Name='CustomFooterChk' Style='{StaticResource Switch}' Content='页脚信息'/>
              </WrapPanel>
            </StackPanel>
          </WrapPanel>
        </WrapPanel>
        <WrapPanel Margin='0,2,0,0'>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='圆角' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleRadiusVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleRadiusSlider' Width='156' Minimum='0' Maximum='28' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='尾巴' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleTailVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleTailSlider' Width='156' Minimum='8' Maximum='24' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='不透明度' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleOpacityVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleOpacitySlider' Width='156' Minimum='0.5' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='阴影' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleShadowVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleShadowSlider' Width='156' Minimum='0' Maximum='0.6' Style='{StaticResource Flat}'/>
          </StackPanel>
        </WrapPanel>
        <WrapPanel x:Name='BubbleGlassPanel' Orientation='Horizontal' Margin='0,2,0,0' Visibility='Collapsed'>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='玻璃透明度' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='GlassOpacityVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='GlassOpacitySlider' Width='156' Minimum='0.15' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='高光' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='GlassHighlightVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='GlassHighlightSlider' Width='156' Minimum='0' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='边框' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='GlassBorderVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='GlassBorderSlider' Width='156' Minimum='0' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
        </WrapPanel>
        <WrapPanel x:Name='BubbleMonetPanel' Orientation='Horizontal' Margin='0,2,0,0' Visibility='Collapsed'>
          <TextBlock Text='莫奈取色' FontSize='10' Foreground='__MUTED__' VerticalAlignment='Center' Margin='1,0,8,0'/>
          <Border x:Name='MonetSwatch' Width='28' Height='28' CornerRadius='9' BorderThickness='1' Margin='0,0,7,0'/>
          <TextBlock x:Name='MonetColorVal' FontSize='10' Foreground='__FGLABEL__' VerticalAlignment='Center' MinWidth='74'/>
          <Button x:Name='MonetPickBtn' Content='选择取色…' Width='86' Style='{StaticResource Btn}' Margin='0,0,6,0'/>
          <Button x:Name='MonetAutoBtn' Content='跟随壁纸' Width='76' Style='{StaticResource Btn}'/>
        </WrapPanel>
        <TextBlock Style='{StaticResource Sect}' Text='宠物显示'/>
        <Grid Margin='0,9,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/><ColumnDefinition Width='60'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='大小'/>
          <Slider x:Name='ScaleSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.4' Maximum='2.6'
                  ToolTip='只调整当前这只桌宠；也可以对任意一只直接 Ctrl+滚轮。点「全部」同步给所有桌宠。'/>
          <TextBlock x:Name='ScaleVal' Grid.Column='2' Style='{StaticResource Val}' Text='1.00x'/>
          <Button x:Name='ScaleAllBtn' Grid.Column='3' Content='全部' Width='52' Height='22'
                  HorizontalAlignment='Right' VerticalAlignment='Center' Style='{StaticResource Btn}'
                  ToolTip='把当前大小同步给所有桌宠，并作为之后新分身的默认大小'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='不透明度'/>
          <Slider x:Name='OpacitySlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.4' Maximum='1'/>
          <TextBlock x:Name='OpacityVal' Grid.Column='2' Style='{StaticResource Val}' Text='100%'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='帧率'/>
          <Slider x:Name='FpsSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='15' Maximum='120' IsSnapToTickEnabled='True' TickFrequency='1'/>
          <TextBlock x:Name='FpsVal' Grid.Column='2' Style='{StaticResource Val}' Text='25 fps'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='空闲帧率'/>
          <Slider x:Name='IdleFpsSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='1' Maximum='30' IsSnapToTickEnabled='True' TickFrequency='1'/>
          <TextBlock x:Name='IdleFpsVal' Grid.Column='2' Style='{StaticResource Val}' Text='4 fps'/>
        </Grid>
        <TextBlock Style='{StaticResource Sect}' Text='动作点播'/>
        <CheckBox x:Name='AutoActionsChk' Style='{StaticResource Switch}' Content='自动动作' Margin='0,0,0,0'/>
        <Border x:Name='ActionPoolCard' Background='__ROW__' CornerRadius='12' Padding='12,10,12,8' Margin='0,6,0,0'>
          <StackPanel>
            <DockPanel>
              <StackPanel Orientation='Horizontal' DockPanel.Dock='Right' VerticalAlignment='Center'>
                <Button x:Name='ActionPoolAllBtn' Content='全选' Width='56' Height='24' FontSize='10.5' Style='{StaticResource Btn}'/>
                <Button x:Name='ActionPoolNoneBtn' Content='清空' Width='56' Height='24' FontSize='10.5' Style='{StaticResource Btn}'/>
              </StackPanel>
              <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                <TextBlock Text='自动动作池' FontSize='11.5' FontWeight='SemiBold' Foreground='__FG2__'/>
                <TextBlock x:Name='ActionPoolCount' Text='已选 0 / 26' FontSize='10' Foreground='__MUTED__' Margin='8,0,0,0' VerticalAlignment='Center'/>
              </StackPanel>
            </DockPanel>
            <TextBlock Text='小动作' FontSize='10' Foreground='__MUTED__' Margin='0,8,0,3'/>
            <WrapPanel x:Name='ActionPoolIdlePanel'/>
            <TextBlock Text='移动' FontSize='10' Foreground='__MUTED__' Margin='0,2,0,3'/>
            <WrapPanel x:Name='ActionPoolMovePanel'/>
            <TextBlock Text='特效' FontSize='10' Foreground='__MUTED__' Margin='0,2,0,3'/>
            <WrapPanel x:Name='ActionPoolFxPanel'/>
            <Grid Margin='0,8,0,0'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
              </Grid.ColumnDefinitions>
              <TextBlock Style='{StaticResource Lbl}' Text='单次时长'/>
              <Slider x:Name='ActionDurationSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.5' Maximum='3'/>
              <TextBlock x:Name='ActionDurationVal' Grid.Column='2' Style='{StaticResource Val}' Text='1.0x'/>
            </Grid>
            <Grid Margin='0,2,0,0'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
              </Grid.ColumnDefinitions>
              <TextBlock Style='{StaticResource Lbl}' Text='动作间隔'/>
              <Slider x:Name='ActionPauseSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='6' Maximum='300'/>
              <TextBlock x:Name='ActionPauseVal' Grid.Column='2' Style='{StaticResource Val}' Text='14s'/>
            </Grid>
            <TextBlock Text='轻微随机变化已自动处理，无需额外设置。' FontSize='9.5'
                       Foreground='__MUTED__' Margin='57,2,0,1'/>
          </StackPanel>
        </Border>
        <Border x:Name='ActionPreviewCard' Margin='0,8,0,0' Height='160' CornerRadius='10' Background='__DESK__' ClipToBounds='True'>
          <Grid>
            <ContentControl x:Name='ActionPreviewHost' HorizontalAlignment='Center' VerticalAlignment='Center' RenderTransformOrigin='0.5,1'>
              <ContentControl.RenderTransform>
                <TransformGroup>
                  <ScaleTransform x:Name='ActionPreviewScale'/>
                  <RotateTransform x:Name='ActionPreviewRotate'/>
                  <TranslateTransform x:Name='ActionPreviewMove'/>
                </TransformGroup>
              </ContentControl.RenderTransform>
            </ContentControl>
            <TextBlock x:Name='ActionPreviewLabel' Text='预览：点击下方动作' FontSize='10' Foreground='__MUTED__' Margin='10,0,0,8' VerticalAlignment='Bottom' HorizontalAlignment='Left'/>
          </Grid>
        </Border>
        <Border x:Name='ActionPinCard' Margin='0,6,0,0' Padding='10,7,10,7' CornerRadius='10' Background='__ROW__'>
          <StackPanel>
            <CheckBox x:Name='ActionPinChk' Style='{StaticResource Switch}' Content='一直执行所选动作'
                      Margin='0' IsEnabled='False'/>
            <TextBlock x:Name='ActionPinHint' Text='先在动作列表中选择一个动作' FontSize='9.5'
                       Foreground='__MUTED__' TextWrapping='Wrap' Margin='42,1,0,0'/>
          </StackPanel>
        </Border>
        <Border x:Name='ActionListCard' Margin='0,8,0,0' Padding='8,7,8,8' CornerRadius='10' Background='__ROW__'>
          <StackPanel>
            <DockPanel Margin='0,0,0,6'>
              <TextBlock x:Name='ActionListHint' Text='动作列表（可滚动）' FontSize='10' Foreground='__MUTED__'/>
            </DockPanel>
            <ScrollViewer MaxHeight='210' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'
                          CanContentScroll='True'>
              <UniformGrid x:Name='ActionGrid' Columns='3'/>
            </ScrollViewer>
          </StackPanel>
        </Border>
        <TextBlock Style='{StaticResource Sect}' Text='外观'/>
        <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,0,0,4'>
          <Button x:Name='RoleEditBtn' Content='编辑' Width='56' Style='{StaticResource Mini}' ToolTip='管理模式：添加 / 删除角色素材'/>
        </StackPanel>
        <DockPanel Margin='0,0,0,4'>
          <Button x:Name='RoleDirBtn' Content='打开素材目录' Width='104' DockPanel.Dock='Right'
                  Style='{StaticResource Mini}' ToolTip='打开配置与角色素材的实际保存目录'/>
          <TextBlock x:Name='RoleLibrarySummary' FontSize='10' Foreground='__MUTED__'
                     VerticalAlignment='Center' TextTrimming='CharacterEllipsis' Margin='1,0,8,0'/>
        </DockPanel>
        <WrapPanel x:Name='RolePanel'/>
        <TextBlock x:Name='RigScopeHint' FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,4,0,2'
                   Text='单图动态是当前 PNG 的专属设置；选中一张单图后，可分别调整它的动态效果。'/>
        <Border x:Name='RigImageCard' Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'
                CornerRadius='12' Padding='12,10,12,9' Margin='0,6,0,2' Visibility='Collapsed'>
          <StackPanel>
            <DockPanel>
              <Border DockPanel.Dock='Right' Background='#4B6BFB' CornerRadius='7' Padding='7,2,7,3' VerticalAlignment='Top'>
                <TextBlock Text='仅当前单图' FontSize='9' Foreground='#FFFFFF'/>
              </Border>
              <StackPanel>
                <TextBlock Text='单图动态' FontSize='11.5' FontWeight='SemiBold' Foreground='__FG2__'/>
                <TextBlock x:Name='RigRoleName' Text='…' FontSize='9.5' Foreground='__MUTED__'
                           TextTrimming='CharacterEllipsis' Margin='0,2,8,0'/>
              </StackPanel>
            </DockPanel>
            <CheckBox x:Name='MeshRigChk' Style='{StaticResource Switch}' Content='启用呼吸 / 歪头 / 眨眼'
                      Margin='0,6,0,0' FontSize='11'
                      ToolTip='只对当前这张 PNG 生效；其他单图可以各自设置'/>
            <Grid Margin='0,2,0,0'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='70'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
              </Grid.ColumnDefinitions>
              <TextBlock Style='{StaticResource Lbl}' Text='动态强度'/>
              <Slider x:Name='RigIntensitySlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0' Maximum='2'/>
              <TextBlock x:Name='RigIntensityVal' Grid.Column='2' Style='{StaticResource Val}' Text='1.0'/>
            </Grid>
            <Grid Margin='0,2,0,0'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='70'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
              </Grid.ColumnDefinitions>
              <TextBlock Style='{StaticResource Lbl}' Text='脸部高度'/>
              <Slider x:Name='RigFaceYSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.10' Maximum='0.50'/>
              <TextBlock x:Name='RigFaceYVal' Grid.Column='2' Style='{StaticResource Val}' Text='0.25'/>
            </Grid>
            <TextBlock FontSize='9.5' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                       Text='这些值只保存在当前这张单图上。眨眼位置不对时调「脸部高度」；GIF、序列帧和导入包不受影响。'/>
          </StackPanel>
        </Border>
        <TextBlock Text='素材工具' FontSize='10' FontWeight='SemiBold' Foreground='__MUTED__' Margin='1,8,0,3'/>
        <StackPanel Margin='0,2,0,0'>
          <Border Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'
                  CornerRadius='10' Padding='11,9,11,10' Margin='0,0,0,7'>
            <StackPanel>
              <DockPanel>
                <Border DockPanel.Dock='Right' Background='__CHIP__' CornerRadius='7' Padding='7,2,7,3'>
                  <TextBlock Text='基础入口' FontSize='9' Foreground='__MUTED__'/>
                </Border>
                <TextBlock Text='导入图片素材' FontSize='11.5' FontWeight='SemiBold' Foreground='__FG2__'/>
              </DockPanel>
              <TextBlock Text='支持 PNG、JPG、GIF、BMP；导入后立即切换为当前外观。'
                         FontSize='9.5' Foreground='__MUTED__' TextWrapping='Wrap' Margin='0,3,0,0'/>
              <DockPanel Margin='0,7,0,0'>
                <Button x:Name='RoleAddBtn' Content='选择图片或动图…' Width='124' Style='{StaticResource Btn}'/>
                <TextBlock x:Name='RoleImportStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='7,0,0,0'
                           TextTrimming='CharacterEllipsis' ToolTip='图片素材导入结果'/>
              </DockPanel>
            </StackPanel>
          </Border>
          <Border Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'
                  CornerRadius='10' Padding='11,9,11,10' Margin='0,0,0,7'>
            <StackPanel>
              <DockPanel>
                <Border DockPanel.Dock='Right' Background='__CHIP__' CornerRadius='7' Padding='7,2,7,3'>
                  <TextBlock Text='图片导入衍生' FontSize='9' Foreground='__MUTED__'/>
                </Border>
                <TextBlock Text='图片转动画' FontSize='11.5' FontWeight='SemiBold' Foreground='__FG2__'/>
              </DockPanel>
              <TextBlock Text='从静态图生成包含待机、行走、点击和完整动作链的逐帧角色。'
                         FontSize='9.5' Foreground='__MUTED__' TextWrapping='Wrap' Margin='0,3,0,0'/>
              <DockPanel Margin='0,7,0,0'>
                <Button x:Name='AnimMakeBtn' Content='由图片生成动画…' Width='132' Style='{StaticResource Btn}'
                        ToolTip='选一张 PNG，自动生成含全部动作的逐帧角色'/>
                <TextBlock x:Name='AnimMakeStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='7,0,0,0'
                           TextTrimming='CharacterEllipsis'/>
              </DockPanel>
            </StackPanel>
          </Border>
          <Border Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'
                  CornerRadius='10' Padding='11,9,11,10'>
            <StackPanel>
              <DockPanel>
                <Border DockPanel.Dock='Right' Background='__CHIP__' CornerRadius='7' Padding='7,2,7,3'>
                  <TextBlock Text='独立功能' FontSize='9' Foreground='__MUTED__'/>
                </Border>
                <TextBlock Text='导入宠物包' FontSize='11.5' FontWeight='SemiBold' Foreground='__FG2__'/>
              </DockPanel>
              <TextBlock Text='可选择单个素材包，也可选择包含多套素材的总目录；程序会自动识别并批量导入。'
                         FontSize='9.5' Foreground='__MUTED__' TextWrapping='Wrap' Margin='0,3,0,0'/>
              <DockPanel Margin='0,7,0,0'>
                <Button x:Name='PackImportBtn' Content='选择素材包或总目录…' Width='148' Style='{StaticResource Btn}'
                        ToolTip='选择单个素材包或上级总目录，程序会自动识别并批量导入'/>
                <TextBlock x:Name='PackStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='7,0,0,0'
                           TextTrimming='CharacterEllipsis'/>
              </DockPanel>
            </StackPanel>
          </Border>
        </StackPanel>
        <CheckBox x:Name='RandomRoleChk' Style='{StaticResource Switch}' Content='启动时随机外观' Margin='0,7,0,0' FontSize='11'/>
        <TextBlock Style='{StaticResource Sect}' Text='行为'/>
        <WrapPanel>
          <CheckBox x:Name='WalkChk' Style='{StaticResource Switch}' Content='持续散步'
                    ToolTip='开启后会一直溜达（短歇后继续）；关闭后原地待着'/>
          <CheckBox x:Name='SnapChk' Style='{StaticResource Switch}' Content='贴边'/>
          <CheckBox x:Name='SnapAutoChk' Style='{StaticResource Switch}' Content='自动贴边'
                    ToolTip='关闭后只有手动把桌宠拖到屏幕边缘才会贴边'/>
          <CheckBox x:Name='SleepChk' Style='{StaticResource Switch}' Content='作息'/>
          <CheckBox x:Name='CollideChk' Style='{StaticResource Switch}' Content='碰撞带动'/>
        </WrapPanel>
        <TextBlock FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='「持续散步」开启后会一直溜达，途中也会随机做动作；关闭后停在原地。'/>
        <DockPanel Margin='0,4,0,0'>
          <CheckBox x:Name='EdgeHideChk' Style='{StaticResource Switch}' Content='边缘隐藏' Margin='0,0,10,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='收起前等待'/>
          <TextBox x:Name='EdgeDelayBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='秒' Margin='5,0,0,0'/>
        </DockPanel>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='贴边露出'/>
          <Slider x:Name='SliverSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.05' Maximum='0.8'/>
          <TextBlock x:Name='SliverVal' Grid.Column='2' Style='{StaticResource Val}' Text='32%'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='吸附距离'/>
          <Slider x:Name='SnapDistanceSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='8' Maximum='200'/>
          <TextBlock x:Name='SnapDistanceVal' Grid.Column='2' Style='{StaticResource Val}' Text='56 px'/>
        </Grid>
        <TextBlock FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='「自动贴边」关闭时，散步、动作和启动都不会吸边；只有手动拖到边缘松手才会贴边。'/>
        <Grid Margin='0,6,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='速度'/>
          <Slider x:Name='SpeedSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='25' Maximum='170'/>
          <TextBlock x:Name='SpeedVal' Grid.Column='2' Style='{StaticResource Val}' Text='72'/>
        </Grid>
        <TextBlock Style='{StaticResource Sect}' Text='台词与互动'/>
        <CheckBox x:Name='QuipsChk' Style='{StaticResource Switch}' Content='启用随机台词' Margin='0,0,0,0'/>
        <Border Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'
                CornerRadius='10' Padding='11,9,11,10' Margin='0,7,0,0'>
          <StackPanel>
            <DockPanel>
              <Grid DockPanel.Dock='Right' Width='224' VerticalAlignment='Center'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='108'/><ColumnDefinition Width='8'/><ColumnDefinition Width='108'/>
                </Grid.ColumnDefinitions>
                <Button x:Name='QuipImportBtn' Content='导入台词库…' Style='{StaticResource Btn}' Margin='0'/>
                <Button x:Name='QuipExportBtn' Grid.Column='2' Content='导出台词库…' Style='{StaticResource Btn}' Margin='0'/>
              </Grid>
              <TextBlock Text='台词库' FontSize='11.5' FontWeight='SemiBold' Foreground='__FG2__'
                         VerticalAlignment='Center'/>
            </DockPanel>
            <TextBlock x:Name='QuipStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='0,7,0,0'
                       Text='使用内置台词库'
                       TextTrimming='CharacterEllipsis' ToolTip='JSON：{ 「打招呼」: [「…」], 「蹦一下」: [「…」] }；也认英文动作名，导入后自动去重合并'/>
          </StackPanel>
        </Border>
        <TextBlock Style='{StaticResource Sect}' Text='余额接口'/>
        <DockPanel Margin='0,0,0,7'>
          <Button x:Name='RefreshBtn' DockPanel.Dock='Right' Content='刷新数据' Width='78' Style='{StaticResource Mini}' Margin='0'/>
          <TextBlock Text='余额与查询状态' FontSize='10' Foreground='__MUTED__' VerticalAlignment='Center'/>
        </DockPanel>
        <StackPanel x:Name='ProviderList'/>
        <StackPanel x:Name='ProviderKeyList'/>
        
        <WrapPanel x:Name='UnsupportedPanel'/>
        <WrapPanel Margin='0,5,0,2'>
          <CheckBox x:Name='AlertChk' Style='{StaticResource Switch}' Content='余额预警'/>
          <CheckBox x:Name='BudgetChk' Style='{StaticResource Switch}' Content='预算提醒'/>
        </WrapPanel>
        <StackPanel x:Name='ProviderAlertList' Margin='0,4,0,0'/>
        <TextBlock FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='按平台分别设置余额下限和今日预算；留空 = 该平台不提醒。'/>
        <DockPanel x:Name='ProxyRow' Margin='0,6,0,0'>
          <TextBlock Style='{StaticResource Lbl}' Text='HTTPS 代理' Width='66'/>
          <TextBox x:Name='ProxyBox' Style='{StaticResource TxtBox}'/>
        </DockPanel>
        <TextBlock x:Name='ProxyHint' FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='留空 = 直连；填 http://127.0.0.1:7890 之类的地址可让查询走指定代理（网络出口被限制 HTTP 451 时很有用）。'/>
        <TextBlock Style='{StaticResource Sect}' Text='套餐额度'/>
        <StackPanel x:Name='PlanList'/>
        <StackPanel x:Name='PlanKeyList'/>
        <WrapPanel Margin='0,5,0,2'>
          <CheckBox x:Name='QuotaChk' Style='{StaticResource Switch}' Content='套餐额度提醒'/>
        </WrapPanel>
        <StackPanel x:Name='PlanAlertList' Margin='0,4,0,0'/>
        <TextBlock FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='按套餐分别设置 5 小时和 7 天窗口的提醒阈值；留空 = 该套餐不提醒。'/>
        <TextBlock Style='{StaticResource Sect}' Text='分身'/>
        <DockPanel Margin='0,0,0,6'>
          <CheckBox x:Name='CloneAutoChk' Style='{StaticResource Switch}' Content='自动分裂' Margin='0,0,10,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='上限'/>
          <TextBox x:Name='CloneMaxBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='只（0 = 自动上限 20）' Margin='5,0,0,0'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,6'>
          <CheckBox x:Name='CloneCycleChk' Style='{StaticResource Switch}' Content='循环涨落' Margin='0,0,10,0'
                    ToolTip='到上限后逐只回落到下限，停一下再逐只涨回上限'/>
          <TextBlock Style='{StaticResource Lbl}' Text='下限'/>
          <TextBox x:Name='CloneMinBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='只（到上限后逐只回落到下限，再逐只涨回去）' Margin='5,0,0,0' FontSize='10'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,6'>
          <TextBlock Style='{StaticResource Lbl}' Text='分裂外观' Width='56'/>
          <Button x:Name='CloneRoleBtn' Content='分裂外观 · 随机 ▾' Width='210' Style='{StaticResource Btn}'/>
        </DockPanel>
        <TextBlock x:Name='CloneRoleHint' Text='新分身会随机选择角色。' FontSize='9.5'
                   Foreground='__MUTED__' TextWrapping='Wrap' Margin='57,0,0,5'/>
        <StackPanel x:Name='CloneSlotRolePanel' Visibility='Collapsed' Margin='57,0,0,6'/>
        <Grid Margin='0,2,0,6'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='86'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='分裂间隔'/>
          <Slider x:Name='ClonePauseSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='10' Maximum='600'/>
          <TextBlock x:Name='ClonePauseVal' Grid.Column='2' Style='{StaticResource Val}' HorizontalAlignment='Right' Text='60s' ToolTip='基础间隔；实际间隔会加上右侧的随机幅度'/>
        </Grid>
        <Grid Margin='0,2,0,6'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='86'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='间隔随机'/>
          <Slider x:Name='CloneJitterSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0' Maximum='150'/>
          <TextBlock x:Name='CloneJitterVal' Grid.Column='2' Style='{StaticResource Val}' HorizontalAlignment='Right' Text='+50%'/>
        </Grid>
        <DockPanel Margin='2,4,0,0'>
          <StackPanel DockPanel.Dock='Right' Orientation='Horizontal' VerticalAlignment='Center'>
            <Button x:Name='CloneAddBtn' Content='来一个' Width='72' Height='28' Margin='0,0,6,0' Style='{StaticResource Btn}'/>
            <Button x:Name='CloneRemoveBtn' Content='收一个' Width='72' Height='28' Margin='0' Style='{StaticResource Btn}'/>
          </StackPanel>
          <TextBlock x:Name='CloneCount' Style='{StaticResource Lbl}' Text='1 只' VerticalAlignment='Center'/>
        </DockPanel>
        <TextBlock Style='{StaticResource Sect}' Text='应用更新'/>
        <Border CornerRadius='12' Padding='14' Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'>
          <Grid>
            <Grid.RowDefinitions>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='Auto'/>
            </Grid.RowDefinitions>
            <DockPanel>
              <Button x:Name='CheckUpdateBtn' DockPanel.Dock='Right' Content='检查更新' Width='92' Height='30'
                      VerticalAlignment='Top' Style='{StaticResource Btn}'
                      ToolTip='检查并安装公开 Release 的最新版本'/>
              <StackPanel>
                <TextBlock Text='当前版本' FontSize='10' Foreground='__MUTED__'/>
                <TextBlock x:Name='UpdateVersionText' Text='v…' FontSize='24' FontWeight='Bold'
                           Foreground='__FG__' Margin='0,2,0,0' HorizontalAlignment='Left'/>
              </StackPanel>
            </DockPanel>
            <TextBlock x:Name='UpdateStatusText' Grid.Row='1' Text='尚未检查' FontSize='11'
                       Foreground='__MUTED__' Margin='0,8,0,0' TextWrapping='Wrap'/>
            <ProgressBar x:Name='UpdateProgressBar' Grid.Row='2' Height='4' Minimum='0' Maximum='100'
                         Background='__TRACK__' Foreground='#4B6BFB' BorderThickness='0'
                         Margin='0,10,0,2' Visibility='Collapsed'>
              <ProgressBar.Template>
                <ControlTemplate TargetType='ProgressBar'>
                  <Grid>
                    <Border Background='{TemplateBinding Background}' CornerRadius='2'/>
                    <Border x:Name='PART_Indicator' HorizontalAlignment='Left' CornerRadius='2'
                            Background='{TemplateBinding Foreground}'/>
                  </Grid>
                </ControlTemplate>
              </ProgressBar.Template>
            </ProgressBar>
            <TextBlock x:Name='UpdateNotesText' Grid.Row='3' Text='检查更新后可在这里查看版本说明。'
                       FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='0,10,0,0'/>
          </Grid>
        </Border>
        <Border CornerRadius='10' Padding='12,10' Margin='0,8,0,0'
                Background='__NAV__' BorderBrush='__BORDER__' BorderThickness='1'>
          <StackPanel>
            <TextBlock Text='升级说明' FontSize='10.5' FontWeight='SemiBold' Foreground='__FG2__'/>
            <TextBlock Text='安装器来自公开 Release，下载后会校验 SHA-256；升级时等待旧进程退出并保留现有配置。'
                       FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='0,4,0,0'/>
          </StackPanel>
        </Border>
        </StackPanel>
      </Grid>
    </Border>
  </Grid>
</Window>";

        private Config cfg;
        private readonly Config liveCfg;
        private readonly string draftConfigPath;
        private string savedConfigSnapshot;
        private readonly Engine engine;
        private bool savingConfig;
        private Window root;
        private Grid panelShadowLayer;       // 面板阴影层（挖掉卡片形状，只露轮廓外一圈）
        private Border panelShadowShape;
        private Border panelCard;
        private const double PanelShadowPad = 40;
        private readonly Dictionary<string, FrameworkElement> ui = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, TextBlock> statusById = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, PasswordBox> keyBoxes = new Dictionary<string, PasswordBox>();
        private readonly Dictionary<string, PasswordBox> platformTokenBoxes = new Dictionary<string, PasswordBox>();
        private readonly DispatcherTimer previewTimer = new DispatcherTimer();
        private readonly DispatcherTimer actionPreviewTimer = new DispatcherTimer();
        private readonly DispatcherTimer uiGlassRefreshTimer = new DispatcherTimer();
        private DateTime actionPreviewStart;
        private string actionPreviewKey = "";
        private double actionPreviewSeconds = 1.0;
        private ContentControl actionPreviewHost;
        private TextBlock actionPreviewLabel;
        private ScaleTransform actionPreviewScale;
        private RotateTransform actionPreviewRotate;
        private TranslateTransform actionPreviewMove;
        private string actionPreviewRole = "";
        private PackDirector actionPreviewDirector;
        private PetPack actionPack;
        private string panelQuote = "余额、套餐与设置，都在这里。";
        private string panelWeather = "";
        private string lastPanelQuote = "";
        private int panelQuoteRequest;
        private int panelWeatherRequest;
        private static readonly string[] MoveActionKeys = new string[] { "pace", "peek", "stroll", "dash", "follow" };
        private static readonly string[] FxActionKeys = new string[] { "fireworks", "hearts", "sparkles", "confetti" };
        private static readonly string[] PanelQuoteFallbacks = new string[]
        {
            "省一点是一点，余额我盯着呢。",
            "先看余额，再做决定。",
            "今天的额度，也要花在刀刃上。",
            "账单不吓人，心里才有底。",
            "慢一点花，快一点赚。",
            "余额有数，做事不慌。",
            "每一笔消耗，都值得被看见。",
            "省下来的，不只是数字。",
            "今天也一起省点 token 吧。",
            "价格会变，余额会变，记得及时刷新。",
            "钱要花在值得的地方。",
            "今日花费，今日明白。",
            "少一点冲动，多一点余额。",
            "把预算放在眼前，把焦虑留在身后。",
            "数字会说话，余额最诚实。",
            "计划消费，也计划快乐。",
            "不为一时的冲动买单。",
            "每一次刷新，都是对钱包的关心。",
            "该省省，该花花。",
            "余额不是限制，是选择的空间。",
            "看得见的消耗，才好做决定。",
            "今天的克制，是明天的从容。",
            "省下的 token，也能积少成多。",
            "别让预算只活在想象里。",
            "先了解花费，再安排生活。",
            "账算清楚，心就轻松。",
            "用量有边界，创造没有。",
            "为重要的东西保留余量。",
            "每一份额度，都有它的去处。",
            "消耗是过程，余额是底气。",
            "花钱之前，先听听数字怎么说。",
            "小账认真算，大账不慌张。",
            "预算不是束缚，是方向盘。",
            "清楚每一笔，才算真正拥有它。",
            "今天的余额，也在认真生活。",
            "留一点余地，给下一次灵感。",
            "把复杂的数据，变成简单的决定。",
            "适可而止，是一种高级的慷慨。",
            "钱花出去了，经验要留下。",
            "不追最低价，只求最值得。",
            "给欲望设个上限，给喜欢留个位置。",
            "账本不必完美，心里要有数。",
            "省下来的每一分，都在为未来投票。",
            "今天的用量，别忘了看看。",
            "世界很大，预算要稳。",
            "认真花钱，也认真攒钱。"
        };
        private sealed class NumericControlSpec
        {
            public string SliderName;
            public string ValueName;
            public string Label;
            public double Step;

            public NumericControlSpec(string sliderName, string valueName, string label, double step)
            {
                SliderName = sliderName;
                ValueName = valueName;
                Label = label;
                Step = step;
            }
        }
        private static readonly NumericControlSpec[] NumericControlSpecs = new NumericControlSpec[]
        {
            new NumericControlSpec("BubbleSpeedSlider", "BubbleSpeedVal", "动效速率", 0.1),
            new NumericControlSpec("BubbleDepthSlider", "BubbleDepthVal", "动效深度", 0.05),
            new NumericControlSpec("BubbleFontSlider", "BubbleFontVal", "气泡字号", 0.05),
            new NumericControlSpec("BubbleWidthSlider", "BubbleWidthVal", "自定义宽度", 4),
            new NumericControlSpec("BubbleHeightSlider", "BubbleHeightVal", "自定义高度", 4),
            new NumericControlSpec("BubbleRadiusSlider", "BubbleRadiusVal", "圆角", 1),
            new NumericControlSpec("BubbleTailSlider", "BubbleTailVal", "尾巴大小", 1),
            new NumericControlSpec("BubbleOpacitySlider", "BubbleOpacityVal", "气泡不透明度", 0.02),
            new NumericControlSpec("BubbleShadowSlider", "BubbleShadowVal", "阴影强度", 0.02),
            new NumericControlSpec("GlassOpacitySlider", "GlassOpacityVal", "玻璃透明度", 0.02),
            new NumericControlSpec("GlassHighlightSlider", "GlassHighlightVal", "玻璃高光", 0.02),
            new NumericControlSpec("GlassBorderSlider", "GlassBorderVal", "玻璃边框", 0.02),
            new NumericControlSpec("ScaleSlider", "ScaleVal", "宠物大小", 0.05),
            new NumericControlSpec("OpacitySlider", "OpacityVal", "宠物不透明度", 0.02),
            new NumericControlSpec("FpsSlider", "FpsVal", "活动帧率", 1),
            new NumericControlSpec("IdleFpsSlider", "IdleFpsVal", "空闲帧率", 1),
            new NumericControlSpec("ActionDurationSlider", "ActionDurationVal", "单次时长", 0.1),
            new NumericControlSpec("ActionPauseSlider", "ActionPauseVal", "动作间隔", 1),
            new NumericControlSpec("RigIntensitySlider", "RigIntensityVal", "动态强度", 0.05),
            new NumericControlSpec("RigFaceYSlider", "RigFaceYVal", "脸部高度", 0.01),
            new NumericControlSpec("SliverSlider", "SliverVal", "贴边露出", 0.01),
            new NumericControlSpec("SnapDistanceSlider", "SnapDistanceVal", "吸附距离", 4),
            new NumericControlSpec("SpeedSlider", "SpeedVal", "移动速度", 5),
            new NumericControlSpec("ClonePauseSlider", "ClonePauseVal", "分裂间隔", 10),
            new NumericControlSpec("CloneJitterSlider", "CloneJitterVal", "间隔随机", 5)
        };
        private readonly Dictionary<string, FrameworkElement> numericSteppers =
            new Dictionary<string, FrameworkElement>();
        private sealed class DetectedWeatherLocation
        {
            public string Label = "";
            public double Latitude;
            public double Longitude;
        }
        private double previewPhase;
        private bool updating;
        private bool updatingActionPin;
        private bool roleEditMode;   // 外观页「编辑」开关：只有打开时才显示添加/删除
        private int deviceHz = 60;          // 当前显示器刷新率：帧率滑杆的上限就是它
        private Border previewShadowShape;  // 预览的阴影本体（挖掉卡片形状，只露外面一圈）
        private Grid previewShadowLayer;
        private const double PreviewShadowPad = 40;
        private double previewShadowW = -1;
        private double previewShadowH = -1;
        private double previewShadowRadius = -1;
        // 代码里生成的行（数据源、套餐用量、输入框等）也跟随主题配色。
        private Color cRow;
        private Color cField;
        private Color cBorder;
        private Color cFg;
        private Color cFg2;
        private Color cMuted;
        private Color cOk;
        private Color cErr;
        private Color cChip;
        private Color cCard;
        // 主题色只保留"当前这一块"：对勾 + 光晕标识选中；面板重建后由 BuildColorPalette 重新创建。
        private Grid themeSwatchBox;
        private Border themeSwatch;
        private Path themeSwatchCheck;
        private TextBlock themeSwatchHex;

        public PetWindow ActivePet;
        public bool Pinned;
        public DateTime LastHiddenAt = DateTime.MinValue;

        public SettingsPanel(Config config, Engine engineRef)
        {
            liveCfg = config;
            draftConfigPath = AppPaths.DraftConfigPath;
            savedConfigSnapshot = config.SerializeSnapshot();
            cfg = File.Exists(draftConfigPath)
                ? Config.Load(draftConfigPath)
                : config.Clone(draftConfigPath);
            cfg.Saved += delegate
            {
                if (!savingConfig && !updating)
                {
                    UpdateSaveState(HasDraftChanges());
                    RefreshPreviewDock();
                }
            };
            engine = engineRef;
            Pinned = false;
            uiGlassRefreshTimer.Interval = TimeSpan.FromMilliseconds(180);
            uiGlassRefreshTimer.Tick += delegate
            {
                uiGlassRefreshTimer.Stop();
                if (string.Equals(cfg.GetString("ui", "theme", "system"),
                        "glass", StringComparison.OrdinalIgnoreCase))
                {
                    RebuildForAccent(true);
                }
            };
        }

        public void Build()
        {
            dockedPreview = null;
            dockedPreviewViewbox = null;
            dockedPreviewParent = null;
            dockedPreviewIndex = -1;
            fxHostShellParent = null;
            fxHostShellIndex = -1;
            // 气泡主色兼作设置卡片的强调色；界面与气泡的深浅主题仍分别读取。
            string accent = cfg.GetStringAt("bubble.color1", "#4B6BFB");
            if (string.IsNullOrEmpty(accent) || !accent.StartsWith("#", StringComparison.Ordinal)) accent = "#4B6BFB";
            root = (Window)XamlReader.Parse(PanelXaml(accent));
            actionPreviewHost = (ContentControl)root.FindName("ActionPreviewHost");
            actionPreviewLabel = (TextBlock)root.FindName("ActionPreviewLabel");
            actionPreviewScale = (ScaleTransform)root.FindName("ActionPreviewScale");
            actionPreviewRotate = (RotateTransform)root.FindName("ActionPreviewRotate");
            actionPreviewMove = (TranslateTransform)root.FindName("ActionPreviewMove");
            actionPreviewRole = "";
            actionPreviewDirector = null;
            actionPack = null;
            actionPreviewTimer.Stop();
            actionPreviewTimer.Interval = TimeSpan.FromMilliseconds(33);
            actionPreviewTimer.Tick -= OnActionPreviewTick;
            actionPreviewTimer.Tick += OnActionPreviewTick;
            panelShadowLayer = (Grid)root.FindName("PanelShadowLayer");
            panelShadowShape = (Border)root.FindName("PanelShadowShape");
            panelCard = (Border)root.FindName("PanelCard");
            if (panelCard != null) panelCard.SizeChanged += delegate { UpdatePanelShadowClip(); };
            if (panelCard != null) panelCard.MouseRightButtonUp += delegate { HidePanel(); };
            if (panelShadowLayer != null)
            {
                panelShadowLayer.IsHitTestVisible = true;
                panelShadowLayer.Background = Brushes.Transparent;
                panelShadowLayer.MouseRightButtonUp += delegate { HidePanel(); };
            }
            foreach (string name in new string[]
            {
                "Header","TitleText","SubText","SaveSettingsBtn","SaveStateText","CheckUpdateBtn","UpdateVersionText","UpdateStatusText","UpdateProgressBar","UpdateNotesText","ActionGrid","ActionListHint","ActionListCard","ActionPoolCard","ActionPinCard","ActionPinChk","ActionPinHint","AutoActionsChk","ActionPreviewHost","ActionPreviewLabel","RolePanel","RoleLibrarySummary","RoleDirBtn","RigScopeHint","RigImageCard","RigRoleName","MeshRigChk","RigIntensitySlider","RigIntensityVal","RigFaceYSlider","RigFaceYVal","ScaleSlider","ScaleVal","ScaleAllBtn",
                "OpacitySlider","OpacityVal","WalkChk","SnapChk","SnapAutoChk","SnapDistanceSlider","SnapDistanceVal","SleepChk","QuipsChk","CollideChk","SpeedSlider","SpeedVal",
                "QuipImportBtn","QuipExportBtn","QuipStatus","PackImportBtn","PackStatus","RoleImportStatus",
                "FpsSlider","FpsVal","IdleFpsSlider","IdleFpsVal",
                "ActionPauseSlider","ActionPauseVal","ActionPoolIdlePanel","ActionPoolMovePanel","ActionPoolFxPanel","ActionPoolCount","ActionPoolAllBtn","ActionPoolNoneBtn","ActionDurationSlider","ActionDurationVal","ProviderList","UnsupportedPanel","RefreshBtn",
                "PlanList","ProviderKeyList","PlanKeyList","ProxyRow","ProxyHint",
                "RandomRoleChk","RoleEditBtn","RoleAddBtn","AnimMakeBtn","AnimMakeStatus","ProxyBox","EdgeHideChk","EdgeDelayBox","ProviderAlertList","PlanAlertList","AlertChk","BudgetChk","QuotaChk",
                "SliverSlider","SliverVal",
                "ContentRoot","PageScroll","PageSource","Main","Nav","PreviewDock","PreviewDockTitle","PreviewVisualHost","PreviewSummaryHost","BubblePreviewCanvasHost","ActionPreviewCard",
                "BubblePresetBtn","BubbleLayoutBtn","UiThemeDropBtn","ThemeDropBtn","WeatherCityBox","WeatherLocateBtn","WeatherChk","EffectDropBtn","FontDropBtn","NumberFontDropBtn","TrendRangeBtn","BubbleTrendChk","BubbleTailChk","BubbleSpeedSlider","BubbleSpeedVal",
                "BubbleReverseChk","BubbleDepthSlider","BubbleDepthVal","BubbleFontSlider","BubbleFontVal",
                "BubbleCustomSizePanel","CustomSplitChk","CustomHeaderChk","CustomIconChk","CustomHeroChk","CustomDetailChk","CustomMetricsChk","CustomChartChk","CustomQuipChk","CustomFooterChk","BubbleWidthSlider","BubbleWidthVal","BubbleHeightSlider","BubbleHeightVal","BubbleRadiusSlider","BubbleRadiusVal","BubbleTailSlider",
                "BubbleOpacitySlider","BubbleOpacityVal","BubbleShadowSlider","BubbleGlassPanel","GlassOpacitySlider","GlassOpacityVal","GlassHighlightSlider","GlassHighlightVal","GlassBorderSlider","GlassBorderVal","BubbleMonetPanel","MonetSwatch","MonetColorVal","MonetPickBtn","MonetAutoBtn","Color1Panel",
                "MixPanel",
                "FxHost","FxHint","FxCanvas","FxCard","FxIconTile","FxDot","FxTitle","FxAmount","FxRow","FxMeterTrack","FxMeterFill","FxTail","BubbleShadowVal",
                "PickThemeBtn","MixAddBtn",
                "CloneAutoChk","CloneMaxBox","CloneAddBtn","CloneRemoveBtn","CloneCount","QuitBtn"
                ,"ClonePauseSlider","ClonePauseVal","CloneJitterSlider","CloneJitterVal"
                ,"CloneCycleChk","CloneMinBox","CloneRoleBtn","CloneRoleHint","CloneSlotRolePanel"
            })
            {
                ui[name] = (FrameworkElement)root.FindName(name);
            }
            root.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) HidePanel(); };
            ((FrameworkElement)ui["Header"]).MouseLeftButtonDown += delegate { try { root.DragMove(); } catch { } };
            ((Button)ui["SaveSettingsBtn"]).Click += delegate { SaveDraftConfig(); };
            ((Button)ui["CheckUpdateBtn"]).Click += delegate { CheckForUpdates(); };
            ((TextBlock)ui["UpdateVersionText"]).Text = "v" + Program.Version;
            SetUpdateStatus("尚未检查");
            SetUpdateNotes("检查更新后可在这里查看版本说明。");
            SetUpdateProgress(-1);


            RefreshActionControls();
            ((Button)ui["ActionPoolAllBtn"]).Click += delegate
            {
                string role = cfg.GetString("", "role", "");
                SetCurrentActionPool(CurrentActionKeys(), role);
            };
            ((Button)ui["ActionPoolNoneBtn"]).Click += delegate
            {
                string role = cfg.GetString("", "role", "");
                SetCurrentActionPool(new List<string>(), role);
            };
            ((Slider)ui["ActionDurationSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("actions.durationScale", Math.Round(e.NewValue, 2));
                cfg.Save();
                UpdateLabel("ActionDurationVal", e.NewValue, "0.0x");
            };
            BuildRoleButtons();
            ((Button)ui["RoleEditBtn"]).Click += delegate
            {
                roleEditMode = !roleEditMode;
                UpdateRoleEditState();
                BuildRoleButtons();
            };
            UpdateRoleEditState();
            BuildProviderRows();
            BuildPlanRows();
            BuildNavPages();
            try
            {
                ModernizeNumericControls();
                RefreshControlAvailability();
            }
            catch (Exception ex)
            {
                Log.Write("modern numeric controls failed: " + ex);
            }

            ((Slider)ui["ScaleSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                PetWindow target = ActivePet;
                if (target == null && engine.Pets.Count > 0) target = engine.Pets[0];
                if (target != null) target.SetScale(e.NewValue, true);
                else cfg.Set("", "scale", Math.Round(e.NewValue, 3));
                ((TextBlock)ui["ScaleVal"]).Text = e.NewValue.ToString("0.00", CultureInfo.InvariantCulture) + "x";
                cfg.Save();
                engine.Bubble.Position(true);
            };
            ((Button)ui["ScaleAllBtn"]).Click += delegate
            {
                double value = PetWindow.ClampScale(((Slider)ui["ScaleSlider"]).Value);
                foreach (PetWindow pet in engine.Pets) pet.SetScale(value, true);
                cfg.Set("", "scale", value);
                ((TextBlock)ui["ScaleVal"]).Text = value.ToString("0.00", CultureInfo.InvariantCulture) + "x";
                cfg.Save();
                engine.Bubble.Position(true);
            };
            ((Slider)ui["OpacitySlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.Set("", "opacity", Math.Round(e.NewValue, 2));
                foreach (PetWindow pet in engine.Pets) pet.Opacity = e.NewValue;
                ((TextBlock)ui["OpacityVal"]).Text = ((int)Math.Round(e.NewValue * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                cfg.Save();
            };
            // 帧率上限 = 当前显示器的刷新率（滑块直接顶到设备极限，比屏幕快没意义）
            deviceHz = Native.RefreshRate();
            ((Slider)ui["FpsSlider"]).Maximum = Math.Max(30, deviceHz);
            ((Slider)ui["FpsSlider"]).ToolTip = "当前显示器最高 " + deviceHz + " Hz";
            // 空闲帧率同样顶到设备上限：想要一直丝滑的人可以直接拉满（代价是更耗电）。
            ((Slider)ui["IdleFpsSlider"]).Maximum = Math.Max(30, deviceHz);
            ((Slider)ui["IdleFpsSlider"]).ToolTip = "当前显示器最高 " + deviceHz + " Hz；拉满会更耗电";
            ((Slider)ui["FpsSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                int fps = (int)Math.Round(e.NewValue);
                cfg.SetAt("frames.activePerSecond", (double)fps);
                ((TextBlock)ui["FpsVal"]).Text = fps.ToString(CultureInfo.InvariantCulture) + " fps";
                cfg.Save();
            };
            ((Slider)ui["IdleFpsSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                int fps = (int)Math.Round(e.NewValue);
                cfg.SetAt("frames.idlePerSecond", (double)fps);
                ((TextBlock)ui["IdleFpsVal"]).Text = fps.ToString(CultureInfo.InvariantCulture) + " fps";
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
                int seconds = (int)Math.Round(e.NewValue);
                cfg.SetAt("actions.pauseMinSec", seconds);
                cfg.SetAt("actions.pauseMaxSec", seconds);
                cfg.Save();
                UpdateActionPauseLabel();
            };
            BindSwitch("AutoActionsChk", "actions", "enabled");
            CheckBox actionPin = (CheckBox)ui["ActionPinChk"];
            actionPin.Checked += delegate { SaveActionPin(true); };
            actionPin.Unchecked += delegate { SaveActionPin(false); };
            BindSwitch("WalkChk", "walk", "enabled");
            BindSwitch("SnapChk", "snap", "enabled");
            BindSwitch("SnapAutoChk", "snap", "auto");
            CheckBox rigChk = (CheckBox)ui["MeshRigChk"];
            RoutedEventHandler rigChanged = delegate
            {
                if (updating) return;
                string role = CurrentSinglePngRole();
                if (string.IsNullOrEmpty(role)) return;
                RigSettings.Set(cfg, role, "mesh", rigChk.IsChecked == true);
                cfg.Save();
                foreach (PetWindow pet in engine.Pets)
                {
                    if (string.Equals(pet.Role, role, StringComparison.Ordinal)) pet.ApplyRole(pet.Role);
                }
            };
            rigChk.Checked += rigChanged;
            rigChk.Unchecked += rigChanged;
            ((Slider)ui["RigIntensitySlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                string role = CurrentSinglePngRole();
                if (string.IsNullOrEmpty(role)) return;
                RigSettings.Set(cfg, role, "intensity", Math.Round(e.NewValue, 2));
                cfg.Save();
                ((TextBlock)ui["RigIntensityVal"]).Text = e.NewValue.ToString("0.0", CultureInfo.InvariantCulture);
            };
            ((Slider)ui["RigFaceYSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                string role = CurrentSinglePngRole();
                if (string.IsNullOrEmpty(role)) return;
                RigSettings.Set(cfg, role, "faceY", Math.Round(e.NewValue, 2));
                cfg.Save();
                ((TextBlock)ui["RigFaceYVal"]).Text = e.NewValue.ToString("0.00", CultureInfo.InvariantCulture);
            };
            ((Slider)ui["SnapDistanceSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("snap.distancePx", Math.Round(e.NewValue));
                cfg.Save();
                UpdateLabel("SnapDistanceVal", Math.Round(e.NewValue), "0 px");
            };
            BindSwitch("SleepChk", "sleep", "enabled");
            BindSwitch("QuipsChk", "alert", "quipsOn");
            BindSwitch("CollideChk", "pets", "collide");
            BindSwitch("AlertChk", "alert", "balanceWarn");
            BindSwitch("BudgetChk", "alert", "budgetWarn");
            BindSwitch("QuotaChk", "alert", "quotaWarn");
            BindSwitch("WeatherChk", "weather", "enabled");
            BindSwitch("EdgeHideChk", "edgeHide", "enabled");
            BindSwitch("CloneAutoChk", "clones", "auto");
            BindSwitch("RandomRoleChk", "roles", "random");
            BindSwitch("BubbleTrendChk", "bubble", "trend");
            BindSwitch("BubbleTailChk", "bubble", "tail");
            BindSwitch("CustomSplitChk", "bubble", "customSplit");
            BindSwitch("CustomHeaderChk", "bubble", "customHeader");
            BindSwitch("CustomIconChk", "bubble", "customIcon");
            BindSwitch("CustomHeroChk", "bubble", "customHero");
            BindSwitch("CustomDetailChk", "bubble", "customDetail");
            BindSwitch("CustomMetricsChk", "bubble", "customMetrics");
            BindSwitch("CustomChartChk", "bubble", "customChart");
            BindSwitch("CustomQuipChk", "bubble", "customQuip");
            BindSwitch("CustomFooterChk", "bubble", "customFooter");
            WireThemeDropdown("UiThemeDropBtn", UiThemeValues, UiThemeLabels, "ui.theme", true);
            WireThemeDropdown("ThemeDropBtn", ThemeValues, ThemeLabels, "bubble.theme", true);
            WireDropdown("EffectDropBtn", EffectValues, EffectLabels, "bubble.effect", true);
            WireDropdown("FontDropBtn", FontValues, FontLabels, "bubble.font", false);
            WireDropdown("NumberFontDropBtn", NumberFontValues, NumberFontLabels, "bubble.numberFont", false);
            WireBubbleLayoutDropdown();
            WireBubblePresetDropdown();
            WireDropdown("TrendRangeBtn", TrendValues, TrendLabels, "bubble.trendDays", true);
            BuildColorPalette("Color1Panel", "bubble.color1", "#4B6BFB");
            ((Button)ui["PickThemeBtn"]).Click += delegate { OpenThemePicker((FrameworkElement)ui["PickThemeBtn"]); };
            ((Button)ui["MixAddBtn"]).Click += delegate
            {
                Pinned = true;
                string initial = cfg.GetStringAt("bubble.color1", "#4B6BFB");
                ShowColorPicker(ui["MixAddBtn"], initial, delegate(string hex)
                {
                    Pinned = true;
                    // 上限之内只保留最近的颜色：加满之后新颜色会顶掉最早设置的那个。
                    List<string> colors = BubbleTheme.MixColors(cfg);
                    if (!colors.Contains(hex)) colors.Add(hex);
                    TrimMix(colors);
                    cfg.SetAt("bubble.colors", colors.ToArray());
                    cfg.Save();
                    engine.Data.Refresh();
                    Update();
                }, true);
            };
            BuildMixPalette();
            ((Slider)ui["BubbleSpeedSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.Set("bubble", "speed", Math.Round(e.NewValue, 2));
                ((TextBlock)ui["BubbleSpeedVal"]).Text = e.NewValue.ToString("0.0", CultureInfo.InvariantCulture) + "x";
                cfg.Save();
                engine.Data.Refresh();
            };
            BindBubbleSlider("BubbleDepthSlider", "bubble.effectDepth", "BubbleDepthVal", "0.0");
            BindBubbleSlider("BubbleFontSlider", "bubble.fontScale", "BubbleFontVal", "0.0x");
            BindBubbleSlider("BubbleWidthSlider", "bubble.customWidth", "BubbleWidthVal", "0");
            BindBubbleSlider("BubbleHeightSlider", "bubble.customHeight", "BubbleHeightVal", "0");
            BindBubbleSlider("BubbleRadiusSlider", "bubble.radius", "BubbleRadiusVal", "0");
            BindBubbleSlider("BubbleTailSlider", "bubble.tailSize", "BubbleTailVal", "0");
            BindBubbleSlider("BubbleOpacitySlider", "bubble.opacity", "BubbleOpacityVal", "0%");
            BindBubbleSlider("BubbleShadowSlider", "bubble.shadow", "BubbleShadowVal", "0.00");
            BindBubbleSlider("GlassOpacitySlider", "bubble.glassOpacity", "GlassOpacityVal", "0%");
            BindBubbleSlider("GlassHighlightSlider", "bubble.glassHighlight", "GlassHighlightVal", "0%");
            BindBubbleSlider("GlassBorderSlider", "bubble.glassBorder", "GlassBorderVal", "0%");
            ((Button)ui["MonetPickBtn"]).Click += delegate
            {
                Pinned = true;
                string initial = cfg.GetString("bubble", "monetCustomColor", "#4B6BFB");
                if (string.IsNullOrEmpty(initial)) initial = "#4B6BFB";
                ShowColorPicker((FrameworkElement)ui["MonetPickBtn"], initial, delegate(string hex)
                {
                    Pinned = true;
                    cfg.Set("bubble", "monetCustomColor", hex);
                    cfg.Save();
                    engine.RefreshBubblePresentation();
                    RebuildForAccent(true);
                });
            };
            ((Button)ui["MonetAutoBtn"]).Click += delegate
            {
                cfg.Set("bubble", "monetCustomColor", "");
                cfg.Save();
                engine.RefreshBubblePresentation();
                RebuildForAccent(true);
            };
            ((Button)ui["MixAddBtn"]).ToolTip = "从调色盘取色；超过上限会自动顶掉最早设置的颜色";
            ((Button)ui["QuipImportBtn"]).Click += delegate { ImportQuipsDialog(); };
            ((Button)ui["QuipExportBtn"]).Click += delegate { ExportQuipsDialog(); };
            ((Button)ui["RoleAddBtn"]).Click += delegate { AddRoleAssetDialog(); };
            ((Button)ui["RoleDirBtn"]).Click += delegate { OpenRoleDirectory(); };
            ((Button)ui["AnimMakeBtn"]).Click += delegate { MakeAnimDialog(); };
            ((Button)ui["PackImportBtn"]).Click += delegate { ImportPackDialog(); };
            ((Slider)ui["SliverSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("edgeHide.sliverRatio", Math.Round(e.NewValue, 2));
                ((TextBlock)ui["SliverVal"]).Text = ((int)Math.Round(e.NewValue * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                cfg.Save();
                // 已经收起来的桌宠立刻按新比例重新收一次，改完马上能看到
                foreach (PetWindow pet in engine.Pets)
                {
                    if (pet.EdgeHidden) pet.StartEdgeSlide(true);
                }
            };
            BindSwitch("BubbleReverseChk", "bubble", "effectReverse");
            previewTimer.Interval = TimeSpan.FromMilliseconds(110);
            previewTimer.Tick += delegate
            {
                if (!IsVisible) return;
                double speed = cfg.GetNumber("bubble", "speed", 1.0);
                if (speed < 0.1) speed = 0.1;
                previewPhase += 0.045 * speed;
                ApplyPreviewBrush();
            };
            previewTimer.Start();
            BindNumberBox("EdgeDelayBox", "edgeHide.delaySec");
            BindNumberBox("CloneMaxBox", "clones.max");
            ((Slider)ui["ClonePauseSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                int seconds = (int)Math.Round(e.NewValue);
                cfg.SetAt("clones.pauseMinSec", seconds);
                cfg.SetAt("clones.pauseMaxSec", seconds);
                cfg.Save();
                UpdateClonePauseLabel();
            };
            ((Slider)ui["CloneJitterSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("clones.jitter", Math.Round(e.NewValue / 100.0, 2));
                cfg.Save();
                ((TextBlock)ui["CloneJitterVal"]).Text = "+" + ((int)Math.Round(e.NewValue)).ToString(CultureInfo.InvariantCulture) + "%";
                UpdateClonePauseLabel();
            };
            BindSwitch("CloneCycleChk", "clones", "cycle");
            BindNumberBox("CloneMinBox", "clones.min");
            BindField("ProxyBox", "network.proxy");
            ((TextBox)ui["WeatherCityBox"]).LostFocus += delegate
            {
                if (!updating) SaveWeatherCity();
            };
            ((TextBox)ui["WeatherCityBox"]).KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && !updating) SaveWeatherCity();
            };
            ((Button)ui["WeatherLocateBtn"]).Click += delegate
            {
                if (!updating) LocateWeatherAsync();
            };
            ((CheckBox)ui["WeatherChk"]).Click += delegate
            {
                if (!updating) RefreshPanelWeather();
            };
            WireCloneRoleDropdown();
            ((Button)ui["RefreshBtn"]).Click += delegate { engine.Data.Refresh(); };
            ((Button)ui["CloneAddBtn"]).Click += delegate { engine.AddClone(); Update(); };
            ((Button)ui["CloneRemoveBtn"]).Click += delegate { engine.RemoveClone(); Update(); };
            ((Button)ui["QuitBtn"]).Click += delegate
            {
                if (ConfirmDialog("退出桌宠", "退出后桌宠与余额提醒都会停止运行。", "退出")) engine.Quit();
            };
        }

        private void SetUpdateStatus(string text)
        {
            TextBlock status = ui.ContainsKey("UpdateStatusText")
                ? ui["UpdateStatusText"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
        }

        private void SetUpdateNotes(string text)
        {
            TextBlock notes = ui.ContainsKey("UpdateNotesText")
                ? ui["UpdateNotesText"] as TextBlock : null;
            if (notes == null) return;
            notes.Text = string.IsNullOrEmpty(text) ? "此版本没有提供额外说明。" : text;
        }

        private void SetUpdateProgress(int percent)
        {
            ProgressBar progress = ui.ContainsKey("UpdateProgressBar")
                ? ui["UpdateProgressBar"] as ProgressBar : null;
            if (progress == null) return;
            if (percent < 0)
            {
                progress.Visibility = Visibility.Collapsed;
                progress.Value = 0;
                return;
            }
            progress.Visibility = Visibility.Visible;
            progress.Value = Math.Max(0, Math.Min(100, percent));
        }

        private void CheckForUpdates()
        {
            Button button = ui["CheckUpdateBtn"] as Button;
            if (button == null || !button.IsEnabled) return;
            button.IsEnabled = false;
            SetUpdateStatus("检查中…");
            SetUpdateNotes("正在读取公开 Release 信息。");
            SetUpdateProgress(-1);
            Task.Factory.StartNew(delegate
            {
                AvailableUpdate update = null;
                string error = null;
                try
                {
                    update = AppUpdater.Check(cfg);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                root.Dispatcher.BeginInvoke(new Action(delegate
                {
                    button.IsEnabled = true;
                    if (!string.IsNullOrEmpty(error))
                    {
                        SetUpdateStatus("检查失败");
                        SetUpdateNotes(error);
                        MessageBox.Show(root, "检查更新失败：" + error, "检查更新",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (update == null)
                    {
                        SetUpdateStatus("已是最新版本");
                        SetUpdateNotes("当前版本 v" + Program.Version + " 已是公开 Release 的最新版本。");
                        MessageBox.Show(root, "当前已是最新版本 v" + Program.Version + "。",
                            "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    SetUpdateStatus("发现 v" + update.VersionText);
                    SetUpdateNotes(ShortenUpdateNotes(update.Notes, 1600));
                    button.IsEnabled = true;
                    PromptInstallUpdate(update);
                }));
            });
        }

        private void PromptInstallUpdate(AvailableUpdate update)
        {
            string notes = ShortenUpdateNotes(update.Notes, 1200);
            string message = "发现新版本 v" + update.VersionText
                + "，当前版本 v" + Program.Version + "。";
            if (!string.IsNullOrEmpty(notes)) message += "\n\n" + notes;
            message += "\n\n下载安装器并升级？程序会自动退出并在安装完成后重新启动。";
            MessageBoxResult result = MessageBox.Show(root, message, "发现新版本",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) DownloadAndInstallUpdate(update);
        }

        private void DownloadAndInstallUpdate(AvailableUpdate update)
        {
            Button button = ui["CheckUpdateBtn"] as Button;
            if (button != null) button.IsEnabled = false;
            SetUpdateStatus("准备下载");
            SetUpdateNotes("正在下载安装器，完成后会校验文件大小与 SHA-256。");
            SetUpdateProgress(0);
            Task.Factory.StartNew(delegate
            {
                string setupPath = null;
                string error = null;
                try
                {
                    setupPath = AppUpdater.Download(cfg, update, delegate(int percent)
                    {
                        if (root.Dispatcher.HasShutdownStarted) return;
                        root.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            SetUpdateStatus(percent >= 100 ? "校验完成" : "下载 " + percent + "%");
                            SetUpdateProgress(percent);
                        }));
                    });
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                root.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        if (button != null) button.IsEnabled = true;
                        SetUpdateStatus("下载失败");
                        SetUpdateNotes(error);
                        SetUpdateProgress(-1);
                        MessageBox.Show(root, "下载更新失败：" + error, "升级失败",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    try
                    {
                        SetUpdateStatus("正在安装…");
                        SetUpdateNotes("程序即将退出，安装完成后会自动启动新版本。");
                        SetUpdateProgress(100);
                        AppUpdater.LaunchInstaller(setupPath, AppPaths.Root);
                        engine.Quit();
                    }
                    catch (Exception ex)
                    {
                        if (button != null) button.IsEnabled = true;
                        SetUpdateStatus("启动安装器失败");
                        SetUpdateNotes(ex.Message);
                        SetUpdateProgress(-1);
                        MessageBox.Show(root, "启动升级安装器失败：" + ex.Message, "升级失败",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }));
            });
        }

        private static string ShortenUpdateNotes(string notes, int limit)
        {
            if (string.IsNullOrEmpty(notes)) return "";
            string text = notes.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length <= limit) return text;
            return text.Substring(0, limit).TrimEnd() + "…";
        }

        /// <summary>选一个 JSON 文件导入台词：合并去重，不动已有台词。</summary>
        private void ImportQuipsDialog()
        {
            System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = "导入台词（JSON）";
            dialog.Filter = "台词文件 (*.json)|*.json|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            int added = cfg.ImportQuips(dialog.FileName);
            cfg.Save();
            SetQuipStatus(added >= 0
                ? "已导入 " + added + " 句台词"
                : "导入失败：没读到台词（看下 JSON 格式）");
        }

        /// <summary>把当前台词导出成 JSON，改完可以再导入回来。</summary>
        private void ExportQuipsDialog()
        {
            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Title = "导出台词";
            dialog.Filter = "台词文件 (*.json)|*.json";
            dialog.FileName = "quips.json";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            bool ok = cfg.ExportQuips(dialog.FileName);
            SetQuipStatus(ok ? "已导出到 " + System.IO.Path.GetFileName(dialog.FileName) : "导出失败：文件写不出去");
        }

        private void SetQuipStatus(string text)
        {
            TextBlock status = ui.ContainsKey("QuipStatus") ? ui["QuipStatus"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
        }

        /// <summary>
        /// 「图片转动图…」：选一张静态图 → 生成含全部动作的逐帧角色（roles\名字-动图\），
        /// 生成完直接给桌宠换上。生成的是普通 PNG 帧，之后可以自己逐帧精修或替换。
        /// </summary>
        private void MakeAnimDialog()
        {
            System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = "选择要转成动图的角色图片";
            dialog.Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName) + "-动图";
            SetAnimStatus("正在生成…");
            int written = AnimMaker.Build(dialog.FileName, name, 256, null);
            if (written < 0)
            {
                SetAnimStatus("生成失败：读不出这张图片");
                return;
            }
            cfg.Set("", "role", name);
            cfg.SetAt("roles.random", false);
            cfg.Save();
            foreach (PetWindow pet in engine.Pets) pet.ApplyRole(name);
            BuildRoleButtons();
            Update();
            SetAnimStatus("已生成 " + written + " 帧并换上");
        }

        /// <summary>把本地图片/GIF 复制进 roles 目录并立刻换上（重名自动加序号，不覆盖旧素材）。</summary>
        private void AddRoleAssetDialog()
        {
            System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = "选择要添加的角色素材";
            dialog.Filter = "角色图片 (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            try
            {
                System.IO.Directory.CreateDirectory(AppPaths.RolesDir);
                string fileName = System.IO.Path.GetFileName(dialog.FileName);
                string target = System.IO.Path.Combine(AppPaths.RolesDir, fileName);
                if (System.IO.File.Exists(target) || System.IO.Directory.Exists(target))
                {
                    string baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    string extension = System.IO.Path.GetExtension(fileName);
                    int index = 2;
                    while (true)
                    {
                        string candidate = baseName + " (" + index.ToString(CultureInfo.InvariantCulture) + ")" + extension;
                        target = System.IO.Path.Combine(AppPaths.RolesDir, candidate);
                        if (!System.IO.File.Exists(target) && !System.IO.Directory.Exists(target)) break;
                        index++;
                    }
                }
                System.IO.File.Copy(dialog.FileName, target, false);
                string role = System.IO.Path.GetFileName(target);
                cfg.Set("", "role", role);
                cfg.SetAt("roles.random", false);
                cfg.Save();
                foreach (PetWindow pet in engine.Pets) pet.ApplyRole(role);
                BuildRoleButtons();
                Update();
                SetRoleImportStatus("已导入：" + role);
            }
            catch (Exception ex)
            {
                SetRoleImportStatus("图片素材导入失败");
                MessageBox.Show(root, "添加失败：" + ex.Message, "添加角色素材", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenRoleDirectory()
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppPaths.RolesDir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + AppPaths.RolesDir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(root, "打开素材目录失败：" + ex.Message,
                    "素材目录", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportPackDialog()
        {
            Button importButton = ui.ContainsKey("PackImportBtn") ? ui["PackImportBtn"] as Button : null;
            if (importButton != null && !importButton.IsEnabled) return;
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择单个素材包或包含多套素材的总目录";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string source = dialog.SelectedPath;
            bool truncated;
            List<string> candidates = PetPackInstaller.FindPackCandidates(source, out truncated);
            List<string> sources = new List<string>();
            if (candidates.Count == 0) sources.Add(source);
            else sources.AddRange(candidates);
            if (sources.Count > 1)
            {
                string question = "检测到 " + sources.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个素材包。\r\n是否全部导入？";
                if (truncated) question += "\r\n目录过多，本次只处理扫描到的前 64 个。";
                MessageBoxResult answer = MessageBox.Show(root, question, "批量导入素材",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;
            }
            if (importButton != null) importButton.IsEnabled = false;
            SetPackStatus(sources.Count > 1
                ? "正在批量导入 " + sources.Count.ToString(CultureInfo.InvariantCulture) + " 个素材包…"
                : "正在导入宠物包…");
            Task.Factory.StartNew(delegate
            {
                List<string> importedIds = new List<string>();
                List<string> importedNotes = new List<string>();
                List<string> failures = new List<string>();
                foreach (string candidate in sources)
                {
                    string note = "";
                    string error = "";
                    string id = PetPackInstaller.Install(cfg, candidate, out note, out error);
                    if (string.IsNullOrEmpty(id))
                    {
                        failures.Add(System.IO.Path.GetFileName(candidate.TrimEnd('\\', '/'))
                            + "：" + error);
                    }
                    else
                    {
                        importedIds.Add(id);
                        importedNotes.Add(note ?? "");
                    }
                }
                root.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (importButton != null) importButton.IsEnabled = true;
                    if (importedIds.Count == 0)
                    {
                        SetPackStatus(failures.Count > 0 ? failures[0] : "没有可导入的素材包");
                        return;
                    }
                    string currentRole = cfg.GetString("", "role", "");
                    if (sources.Count == 1 || string.IsNullOrEmpty(currentRole))
                    {
                        string activeId = importedIds[0];
                        cfg.Set("", "role", activeId);
                        cfg.SetAt("roles.random", false);
                        cfg.Save();
                        foreach (PetWindow pet in engine.Pets) pet.ApplyRole(activeId);
                    }
                    BuildRoleButtons();
                    RefreshActionControls();
                    Update();
                    string status;
                    if (sources.Count == 1)
                    {
                        string id = importedIds[0];
                        PetPack imported = PetPack.TryLoad(cfg, id);
                        int actionCount = imported == null ? 0 : imported.ActionKeys().Count;
                        status = "已导入宠物包：" + id + "（"
                            + actionCount.ToString(CultureInfo.InvariantCulture) + " 个动作）";
                        if (importedNotes.Count > 0 && !string.IsNullOrEmpty(importedNotes[0]))
                            status += "；" + importedNotes[0];
                    }
                    else
                    {
                        status = "批量导入完成：成功 "
                            + importedIds.Count.ToString(CultureInfo.InvariantCulture) + " 个";
                        if (failures.Count > 0)
                            status += "，失败 " + failures.Count.ToString(CultureInfo.InvariantCulture) + " 个";
                        if (truncated) status += "；目录过多，只扫描了前 64 个";
                        if (!string.IsNullOrEmpty(currentRole)) status += "；当前外观未切换";
                    }
                    SetPackStatus(status);
                    if (sources.Count > 1)
                    {
                        string detail = status;
                        if (failures.Count > 0)
                        {
                            int shown = Math.Min(8, failures.Count);
                            detail += Environment.NewLine + Environment.NewLine
                                + string.Join(Environment.NewLine,
                                    failures.GetRange(0, shown).ToArray());
                            if (failures.Count > shown) detail += Environment.NewLine + "……";
                        }
                        MessageBox.Show(root, detail,
                            failures.Count > 0 ? "批量导入完成，部分失败" : "批量导入完成",
                            MessageBoxButton.OK,
                            failures.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    }
                }));
            });
        }

        private void SetPackStatus(string text)
        {
            TextBlock status = ui.ContainsKey("PackStatus") ? ui["PackStatus"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
        }

        private void SetRoleImportStatus(string text)
        {
            TextBlock status = ui.ContainsKey("RoleImportStatus") ? ui["RoleImportStatus"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
        }

        private void SetAnimStatus(string text)
        {
            TextBlock status = ui.ContainsKey("AnimMakeStatus") ? ui["AnimMakeStatus"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
        }

        private void BindSwitch(string name, string section, string key)
        {
            CheckBox box = (CheckBox)ui[name];
            RoutedEventHandler changed = delegate
            {
                if (updating) return;
                cfg.Set(section, key, box.IsChecked == true);
                if (section == "snap" && box.IsChecked == true)
                {
                    foreach (PetWindow pet in engine.Pets) pet.Snap();
                }
                cfg.Save();
                if (section == "bubble") engine.RefreshBubblePresentation();
                RefreshControlAvailability();
            };
            box.Checked += changed;
            box.Unchecked += changed;
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

        private void BindField(string name, string path)
        {
            TextBox box = (TextBox)ui[name];
            box.LostFocus += delegate { SaveField(path, box.Text); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) SaveField(path, box.Text);
            };
        }

        private void UpdateActionPauseLabel()
        {
            int baseSec = (int)Math.Round(cfg.GetNumberAt("actions.pauseMinSec", 14));
            ((TextBlock)ui["ActionPauseVal"]).Text =
                baseSec.ToString(CultureInfo.InvariantCulture) + "s";
        }

        private void UpdateClonePauseLabel()
        {
            int baseSec = (int)Math.Round(cfg.GetNumberAt("clones.pauseMinSec", 60));
            double jitter = cfg.GetNumberAt("clones.jitter", 0.5);
            if (jitter < 0.005)
            {
                ((TextBlock)ui["ClonePauseVal"]).Text = baseSec.ToString(CultureInfo.InvariantCulture) + "s";
                return;
            }
            int maxSec = (int)Math.Round(baseSec * (1 + jitter));
            ((TextBlock)ui["ClonePauseVal"]).Text = baseSec.ToString(CultureInfo.InvariantCulture) + "~" + maxSec.ToString(CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>Slider bound to a numeric config key; the label keeps a live read-out.</summary>
        private void BindBubbleSlider(string sliderName, string path, string labelName, string format)
        {
            Slider slider = (Slider)ui[sliderName];
            slider.ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt(path, Math.Round(e.NewValue, 3));
                cfg.Save();
                engine.RefreshBubblePresentation();
                engine.Data.Refresh();
                UpdateLabel(labelName, e.NewValue, format);
                RefreshBubblePresetButton();
                if (path.StartsWith("bubble.glass", StringComparison.Ordinal))
                {
                    uiGlassRefreshTimer.Stop();
                    uiGlassRefreshTimer.Start();
                }
            };
        }

        private void RefreshBubblePresetButton()
        {
            if (!ui.ContainsKey("BubblePresetBtn")) return;
            BubbleStylePreset preset = BubbleLayouts.MatchingPreset(cfg);
            if (preset == null)
            {
                preset = new BubbleStylePreset();
                preset.Id = "custom";
                preset.Label = "自定义";
                preset.Layout = cfg.GetString("bubble", "layout", "standard");
                preset.Theme = cfg.GetString("bubble", "theme", "light");
                preset.Effect = cfg.GetString("bubble", "effect", "off");
                preset.Radius = cfg.GetNumber("bubble", "radius", 16);
            }
            ((Button)ui["BubblePresetBtn"]).Content = BuildBubblePresetVisual(preset, false);
            ((Button)ui["BubblePresetBtn"]).ToolTip = "气泡风格：" + preset.Label
                + " · " + ThemeLabelForValue(preset.Theme) + " · " + EffectLabelForValue(preset.Effect);
        }

        private FrameworkElement BuildBubblePresetVisual(BubbleStylePreset preset, bool selected)
        {
            ThemePalette palette = BubbleTheme.Resolve(preset.Theme, cfg);
            Color accent = Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB"));
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });

            Border preview = new Border();
            preview.Width = 48;
            preview.Height = 34;
            preview.CornerRadius = new CornerRadius(Math.Max(4, Math.Min(12, preset.Radius * 0.38)));
            preview.Background = new SolidColorBrush(palette.Card);
            preview.BorderBrush = selected ? new SolidColorBrush(accent) : new SolidColorBrush(palette.Border);
            preview.BorderThickness = new Thickness(selected ? 2 : 1);
            preview.VerticalAlignment = VerticalAlignment.Center;
            preview.HorizontalAlignment = HorizontalAlignment.Left;
            Grid previewContent = new Grid();
            previewContent.Margin = new Thickness(7, 6, 6, 6);
            previewContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DockPanel previewHead = new DockPanel();
            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = new SolidColorBrush(accent);
            dot.Margin = new Thickness(0, 0, 4, 0);
            previewHead.Children.Add(dot);
            Border titleLine = new Border();
            titleLine.Height = 2;
            titleLine.Width = 18;
            titleLine.CornerRadius = new CornerRadius(1);
            titleLine.Background = new SolidColorBrush(palette.Title);
            titleLine.HorizontalAlignment = HorizontalAlignment.Left;
            previewHead.Children.Add(titleLine);
            previewContent.Children.Add(previewHead);
            StackPanel previewLines = new StackPanel();
            previewLines.Margin = new Thickness(0, 4, 0, 0);
            Border amountLine = new Border();
            amountLine.Height = 5;
            amountLine.Width = 30;
            amountLine.CornerRadius = new CornerRadius(2);
            amountLine.Background = new SolidColorBrush(palette.Big);
            amountLine.HorizontalAlignment = HorizontalAlignment.Left;
            previewLines.Children.Add(amountLine);
            Border detailLine = new Border();
            detailLine.Height = 2;
            detailLine.Width = 23;
            detailLine.Margin = new Thickness(0, 3, 0, 0);
            detailLine.Background = new SolidColorBrush(palette.Muted);
            detailLine.HorizontalAlignment = HorizontalAlignment.Left;
            previewLines.Children.Add(detailLine);
            Grid.SetRow(previewLines, 1);
            previewContent.Children.Add(previewLines);
            preview.Child = previewContent;
            row.Children.Add(preview);

            StackPanel copy = new StackPanel();
            copy.VerticalAlignment = VerticalAlignment.Center;
            copy.Margin = new Thickness(4, 0, 3, 0);
            TextBlock name = new TextBlock();
            name.Text = preset.Label;
            name.FontSize = 10.2;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(cFg2);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            TextBlock detail = new TextBlock();
            detail.Text = ThemeLabelForValue(preset.Theme) + " · " + EffectLabelForValue(preset.Effect);
            detail.FontSize = 8.2;
            detail.Foreground = new SolidColorBrush(cMuted);
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            detail.Margin = new Thickness(0, 2, 0, 0);
            copy.Children.Add(name);
            copy.Children.Add(detail);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            TextBlock glyph = OptionGlyph(selected ? "\uE73E" : "\uE70D");
            glyph.HorizontalAlignment = HorizontalAlignment.Right;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Foreground = new SolidColorBrush(selected ? accent : cMuted);
            Grid.SetColumn(glyph, 2);
            row.Children.Add(glyph);
            return row;
        }

        private FrameworkElement BuildBubbleLayoutVisual(string id, bool selected)
        {
            string label = BubbleLayoutLabel(id);
            Color accent = Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB"));
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });

            Border frame = new Border();
            frame.Width = 48;
            frame.Height = 34;
            frame.CornerRadius = new CornerRadius(6);
            frame.Background = new SolidColorBrush(cField);
            frame.BorderBrush = selected ? new SolidColorBrush(accent) : new SolidColorBrush(cBorder);
            frame.BorderThickness = new Thickness(selected ? 2 : 1);
            frame.VerticalAlignment = VerticalAlignment.Center;
            Canvas diagram = new Canvas();
            diagram.Width = 44;
            diagram.Height = 30;
            frame.Child = diagram;
            AddLayoutBlock(diagram, 5, 5, 18, 2, cMuted);
            if (id == "minimal")
            {
                AddLayoutBlock(diagram, 7, 13, 30, 7, accent);
                AddLayoutBlock(diagram, 8, 24, 21, 2, cMuted);
            }
            else if (id == "compact")
            {
                AddLayoutBlock(diagram, 6, 11, 20, 5, cFg2);
                AddLayoutBlock(diagram, 6, 19, 15, 3, cMuted, 85);
                AddLayoutBlock(diagram, 24, 19, 13, 3, cMuted, 85);
            }
            else if (id == "wide")
            {
                AddLayoutBlock(diagram, 5, 11, 15, 12, cFg2, 70);
                AddLayoutBlock(diagram, 24, 11, 14, 3, accent, 120);
                AddLayoutBlock(diagram, 24, 17, 12, 2, cMuted, 100);
                AddLayoutBlock(diagram, 24, 22, 13, 2, cMuted, 80);
            }
            else if (id == "dashboard")
            {
                AddLayoutBlock(diagram, 5, 10, 11, 7, accent, 105);
                AddLayoutBlock(diagram, 20, 10, 17, 7, cFg2, 55);
                AddLayoutBlock(diagram, 5, 20, 11, 5, cMuted, 80);
                AddLayoutBlock(diagram, 20, 20, 17, 5, cMuted, 70);
            }
            else
            {
                AddLayoutBlock(diagram, 5, 10, 19, 5, cFg2);
                AddLayoutBlock(diagram, 5, 18, 9, 4, cMuted, 80);
                AddLayoutBlock(diagram, 17, 18, 9, 4, cMuted, 80);
                AddLayoutBlock(diagram, 28, 10, 10, 12, accent, 65);
            }
            row.Children.Add(frame);

            StackPanel copy = new StackPanel();
            copy.VerticalAlignment = VerticalAlignment.Center;
            copy.Margin = new Thickness(4, 0, 3, 0);
            TextBlock name = new TextBlock();
            name.Text = label;
            name.FontSize = 10.2;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(cFg2);
            TextBlock detail = new TextBlock();
            detail.Text = BubbleLayoutDescription(id);
            detail.FontSize = 8.2;
            detail.Foreground = new SolidColorBrush(cMuted);
            detail.Margin = new Thickness(0, 2, 0, 0);
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            copy.Children.Add(name);
            copy.Children.Add(detail);
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            TextBlock glyph = OptionGlyph(selected ? "\uE73E" : "\uE70D");
            glyph.HorizontalAlignment = HorizontalAlignment.Right;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Foreground = new SolidColorBrush(selected ? accent : cMuted);
            Grid.SetColumn(glyph, 2);
            row.Children.Add(glyph);
            return row;
        }

        private static TextBlock OptionGlyph(string text)
        {
            TextBlock glyph = new TextBlock();
            glyph.Text = text;
            glyph.FontFamily = new FontFamily("Segoe Fluent Icons");
            glyph.FontSize = 10;
            return glyph;
        }

        private static void AddLayoutBlock(Canvas canvas, double left, double top,
            double width, double height, Color color, byte alpha = 255)
        {
            Border block = new Border();
            block.Width = width;
            block.Height = height;
            block.CornerRadius = new CornerRadius(Math.Min(2, height / 2));
            block.Background = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            canvas.Children.Add(block);
        }

        private static string BubbleLayoutLabel(string id)
        {
            int index = Array.IndexOf(BubbleLayoutValues, id);
            return index < 0 ? "自定义" : BubbleLayoutLabels[index];
        }

        private static string BubbleLayoutDescription(string id)
        {
            switch (id)
            {
                case "minimal": return "标题 + 主值 + 摘要";
                case "compact": return "摘要 + 迷你图";
                case "dashboard": return "高密度数据";
                case "wide": return "左右分栏";
                case "custom": return "自选模块与尺寸";
                default: return "完整页面";
            }
        }

        private static string ThemeLabelForValue(string value)
        {
            int index = Array.IndexOf(ThemeValues, value);
            return index < 0 ? "浅色" : ThemeLabels[index];
        }

        private static string EffectLabelForValue(string value)
        {
            int index = Array.IndexOf(EffectValues, value);
            return index < 0 ? "无动效" : EffectLabels[index] + "动效";
        }

        private void UpdateLabel(string labelName, double value, string format)
        {
            if (string.IsNullOrEmpty(labelName)) return;
            TextBlock label = ui.ContainsKey(labelName) ? ui[labelName] as TextBlock : null;
            if (label == null && root != null) label = root.FindName(labelName) as TextBlock;
            if (label == null) return;
            if (format == "0%") label.Text = ((int)Math.Round(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
            else if (format == "0.00") label.Text = value.ToString("0.00", CultureInfo.InvariantCulture);
            else label.Text = value.ToString(format, CultureInfo.InvariantCulture);
        }

        private void SyncBubbleSlider(string sliderName, string labelName, double value, string format)
        {
            ((Slider)ui[sliderName]).Value = value;
            UpdateLabel(labelName, value, format);
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
        // ------------------------------------------------------------ 动作预览
        private bool HasSelectedAction()
        {
            if (string.IsNullOrEmpty(actionPreviewKey)) return false;
            if (actionPack != null) return actionPack.Action(actionPreviewKey) != null;
            return Array.IndexOf(PetWindow.ActionKeys, actionPreviewKey) >= 0;
        }

        private void SyncActionPinUi()
        {
            if (!ui.ContainsKey("ActionPinChk") || !ui.ContainsKey("ActionPinHint")) return;
            string role = cfg.GetString("", "role", "");
            string configured = ActionPinSettings.Action(cfg, role);
            if (string.IsNullOrEmpty(actionPreviewKey)
                && ActionPinSettings.Enabled(cfg, role)
                && !string.IsNullOrEmpty(configured))
            {
                actionPreviewKey = configured;
            }
            bool selected = HasSelectedAction();
            bool enabled = selected && ActionPinSettings.Enabled(cfg, role)
                && string.Equals(configured, actionPreviewKey, StringComparison.Ordinal);
            CheckBox check = (CheckBox)ui["ActionPinChk"];
            TextBlock hint = (TextBlock)ui["ActionPinHint"];
            updatingActionPin = true;
            try
            {
                check.IsEnabled = selected;
                check.IsChecked = enabled;
                CheckBox auto = ui.ContainsKey("AutoActionsChk")
                    ? ui["AutoActionsChk"] as CheckBox
                    : null;
                if (auto != null)
                {
                    if (enabled) auto.IsChecked = true;
                    auto.IsEnabled = !enabled;
                }
            }
            finally
            {
                updatingActionPin = false;
            }
            if (enabled)
            {
                hint.Text = "固定动作：" + ActionLabel(actionPreviewKey)
                    + "。关闭设置页后会连续重复；动作池多选会保留但暂停。";
            }
            else if (selected)
            {
                hint.Text = "开启后会一直重复：" + ActionLabel(actionPreviewKey);
            }
            else
            {
                hint.Text = "先在动作列表中选择一个动作";
            }
            SetActionPoolEditingEnabled(!enabled);
        }

        private void SetActionPoolEditingEnabled(bool enabled)
        {
            foreach (string panelName in new string[]
            {
                "ActionPoolIdlePanel", "ActionPoolMovePanel", "ActionPoolFxPanel"
            })
            {
                if (!ui.ContainsKey(panelName)) continue;
                foreach (object child in ((WrapPanel)ui[panelName]).Children)
                {
                    Button button = child as Button;
                    if (button != null) button.IsEnabled = enabled;
                }
            }
            foreach (string buttonName in new string[] { "ActionPoolAllBtn", "ActionPoolNoneBtn" })
            {
                if (!ui.ContainsKey(buttonName)) continue;
                Button button = ui[buttonName] as Button;
                if (button != null) button.IsEnabled = enabled;
            }
        }

        private void SaveActionPin(bool enabled)
        {
            if (updating || updatingActionPin) return;
            string role = cfg.GetString("", "role", "");
            if (enabled && !HasSelectedAction())
            {
                SyncActionPinUi();
                return;
            }
            ActionPinSettings.Set(cfg, role, actionPreviewKey, enabled);
            if (enabled)
            {
                cfg.SetAt("actions.enabled", true);
                cfg.SetAt("roles.random", false);
                CheckBox auto = ui.ContainsKey("AutoActionsChk") ? ui["AutoActionsChk"] as CheckBox : null;
                if (auto != null) auto.IsChecked = true;
                CheckBox randomRole = ui.ContainsKey("RandomRoleChk")
                    ? ui["RandomRoleChk"] as CheckBox
                    : null;
                if (randomRole != null) randomRole.IsChecked = false;
            }
            cfg.Save();
            DateTime now = DateTime.UtcNow;
            foreach (PetWindow pet in engine.Pets)
            {
                if (enabled && !string.Equals(pet.Role, role, StringComparison.Ordinal))
                {
                    pet.ApplyRole(role);
                }
                pet.LastActionKey = "";
                if (enabled)
                {
                    pet.Untuck();
                    pet.EdgeSide = "";
                    pet.EdgeHidden = false;
                    pet.EdgeHideAt = DateTime.MaxValue;
                    if (pet.Mode == "walk") pet.SetIdle();
                    pet.NextWalkAt = DateTime.MaxValue;
                    pet.NextActionAt = now;
                }
                else
                {
                    pet.NextWalkAt = now;
                    pet.NextActionAt = now.AddSeconds(1);
                }
            }
            SyncActionPinUi();
        }

        private void ResetActionPreview()
        {
            actionPreviewTimer.Stop();
            actionPreviewKey = "";
            actionPreviewStart = DateTime.MinValue;
            actionPreviewSeconds = 1.0;
            actionPreviewRole = "";
            actionPreviewDirector = null;
            if (actionPreviewHost != null) actionPreviewHost.Content = null;
            ResetActionPreviewTransforms();
            SetActionPreviewLabel("预览：点击下方动作");
            SyncActionPinUi();
        }

        private void StartActionPreview(string key, string expectedRole)
        {
            string role = cfg.GetString("", "role", "");
            if (!string.Equals(role, expectedRole, StringComparison.Ordinal)) return;
            PetPack previewPack = PetPack.TryLoad(cfg, role);
            if (previewPack == null) return;
            actionPack = previewPack;
            if (key == "random")
            {
                List<string> pool = CurrentActionPool();
                if (pool.Count == 0) { SetActionPreviewLabel("动作池是空的"); return; }
                if (pool.Count > 1) pool.Remove(actionPreviewKey);
                if (pool.Count == 0) { SetActionPreviewLabel("动作池是空的"); return; }
                key = pool[Program.Rng.Next(pool.Count)];
            }
            actionPreviewKey = key;
            MarkSelectedActionButton(key);
            if (ActionPinSettings.Enabled(cfg, role))
            {
                ActionPinSettings.Set(cfg, role, key, true);
                cfg.Save();
            }
            SyncActionPinUi();
            actionPreviewStart = DateTime.UtcNow;
            double scale = cfg.GetNumber("actions", "durationScale", 1.0);
            if (scale < 0.3) scale = 0.3;
            if (scale > 5) scale = 5;
            PackAction action = previewPack.Action(key);
            actionPreviewSeconds = (action == null ? ActionPreviewSeconds(key) : action.Seconds) * scale;
            ResetActionPreviewTransforms();
            RefreshActionPreviewVisual();
            if (actionPreviewDirector != null) actionPreviewDirector.SetState(key);
            SetActionPreviewLabel("预览：" + ActionLabel(key));
            actionPreviewTimer.Start();
        }

        private void SetActionPreviewLabel(string text)
        {
            if (actionPreviewLabel != null) actionPreviewLabel.Text = text;
        }

        private void OnActionPreviewTick(object sender, EventArgs e)
        {
            if (actionPreviewHost == null) return;
            double elapsed = (DateTime.UtcNow - actionPreviewStart).TotalSeconds;
            double progress = actionPreviewSeconds <= 0 ? 1 : elapsed / actionPreviewSeconds;
            if (progress >= 1)
            {
                actionPreviewTimer.Stop();
                ResetActionPreviewTransforms();
                if (actionPreviewDirector != null) actionPreviewDirector.SetState("idle");
                return;
            }
            if (actionPreviewDirector != null) actionPreviewDirector.Update(0.033, 1.0);
            else ApplyActionPreviewFrame(actionPreviewKey, Math.Max(0, Math.Min(1, progress)), elapsed);
        }

        private void ResetActionPreviewTransforms()
        {
            if (actionPreviewScale == null) return;
            actionPreviewScale.ScaleX = 1;
            actionPreviewScale.ScaleY = 1;
            actionPreviewRotate.Angle = 0;
            actionPreviewMove.X = 0;
            actionPreviewMove.Y = 0;
        }

        private void ApplyActionPreviewFrame(string key, double p, double t)
        {
            ResetActionPreviewTransforms();
            switch (key)
            {
                case "hop": actionPreviewMove.Y = -30 * Math.Sin(Math.PI * p); break;
                case "bounce": actionPreviewMove.Y = -26 * Math.Abs(Math.Sin(3 * Math.PI * p)); break;
                case "spin": actionPreviewRotate.Angle = 360 * p; break;
                case "roll": actionPreviewRotate.Angle = -720 * p; actionPreviewMove.Y = -14 * Math.Sin(Math.PI * p); break;
                case "wobble": actionPreviewRotate.Angle = 14 * Math.Sin(6 * Math.PI * p); break;
                case "nod": actionPreviewMove.Y = -6 * Math.Abs(Math.Sin(3 * Math.PI * p)); actionPreviewRotate.Angle = 3 * Math.Sin(6 * Math.PI * p); break;
                case "shake": actionPreviewMove.X = 7 * Math.Sin(6 * Math.PI * p); actionPreviewRotate.Angle = 4 * Math.Sin(6 * Math.PI * p); break;
                case "wave": actionPreviewRotate.Angle = 11 * Math.Sin(8 * Math.PI * p); actionPreviewMove.Y = -4 * Math.Abs(Math.Sin(4 * Math.PI * p)); break;
                case "clap":
                    {
                        double k = Math.Abs(Math.Sin(8 * Math.PI * p));
                        actionPreviewScale.ScaleX = 1 + 0.12 * k;
                        actionPreviewMove.Y = -3 * k;
                        break;
                    }
                case "stretch":
                    {
                        double k = Math.Sin(Math.PI * p);
                        actionPreviewScale.ScaleY = 1 + 0.18 * k;
                        actionPreviewScale.ScaleX = 1 - 0.10 * k;
                        break;
                    }
                case "yawn":
                    {
                        double k = Math.Sin(Math.PI * p);
                        actionPreviewScale.ScaleY = 1 + 0.22 * k;
                        actionPreviewMove.Y = -5 * k;
                        break;
                    }
                case "shy":
                    {
                        double k = Math.Sin(Math.PI * p);
                        actionPreviewScale.ScaleY = 1 - 0.14 * k;
                        actionPreviewRotate.Angle = -6 * k;
                        actionPreviewMove.Y = 2 * Math.Sin(2 * Math.PI * p);
                        break;
                    }
                case "startle":
                    {
                        double k = Math.Sin(Math.PI * p);
                        actionPreviewScale.ScaleX = 1 + 0.14 * k;
                        actionPreviewScale.ScaleY = 1 + 0.14 * k;
                        actionPreviewMove.Y = -18 * k;
                        actionPreviewRotate.Angle = 3 * Math.Sin(10 * Math.PI * p);
                        break;
                    }
                case "lie":
                case "sleep":
                    {
                        double down = Math.Min(1, p / 0.15);
                        double back = p > 0.85 ? Math.Min(1, (p - 0.85) / 0.15) : 0;
                        double amount = Math.Max(0, down - back);
                        actionPreviewRotate.Angle = 76 * amount;
                        actionPreviewMove.Y = 6 * amount + Math.Sin(t * 1.6) * 1.2 * amount;
                        break;
                    }
                case "look":
                    {
                        bool left = (p > 0.2 && p < 0.5) || (p > 0.7 && p < 0.9);
                        actionPreviewScale.ScaleX = left ? -1 : 1;
                        break;
                    }
                case "greet":
                    if (p < 0.4)
                    {
                        actionPreviewMove.Y = -24 * Math.Sin(Math.PI * (p / 0.4));
                    }
                    else
                    {
                        double q = (p - 0.4) / 0.6;
                        actionPreviewRotate.Angle = 11 * Math.Sin(8 * Math.PI * q);
                        actionPreviewMove.Y = -4 * Math.Abs(Math.Sin(4 * Math.PI * q));
                    }
                    break;
                case "pace": actionPreviewMove.X = 90 * Math.Sin(2 * Math.PI * p); break;
                case "peek":
                    {
                        double out1 = Math.Min(1, p / 0.35);
                        double back2 = p > 0.65 ? Math.Min(1, (p - 0.65) / 0.35) : 0;
                        double amount = Math.Max(0, out1 - back2);
                        actionPreviewMove.X = -70 * amount;
                        actionPreviewMove.Y = -2 * Math.Abs(Math.Sin(8 * Math.PI * p)) * (1 - amount);
                        break;
                    }
                case "stroll": actionPreviewMove.X = 60 * Math.Sin(2 * Math.PI * p); actionPreviewMove.Y = -2 * Math.Abs(Math.Sin(8 * Math.PI * p)); break;
                case "dash": actionPreviewMove.X = (p * 2 - 1) * 110; break;
                case "follow": actionPreviewMove.X = 40 * Math.Sin(2 * Math.PI * p); break;
                case "fireworks": actionPreviewMove.Y = -30 * Math.Sin(Math.PI * p); break;
                case "hearts": actionPreviewScale.ScaleY = 1 - 0.14 * Math.Sin(Math.PI * p); actionPreviewRotate.Angle = -6 * Math.Sin(Math.PI * p); break;
                case "sparkles": actionPreviewRotate.Angle = 14 * Math.Sin(6 * Math.PI * p); break;
                case "confetti": actionPreviewMove.Y = -26 * Math.Abs(Math.Sin(3 * Math.PI * p)); break;
            }
        }

        private static double ActionPreviewSeconds(string key)
        {
            switch (key)
            {
                case "hop": return 0.48;
                case "bounce": return 0.95;
                case "spin": return 0.68;
                case "roll": return 1.2;
                case "wobble": return 1.1;
                case "nod": return 1.2;
                case "shake": return 0.95;
                case "wave": return 1.3;
                case "clap": return 0.95;
                case "stretch": return 0.95;
                case "yawn": return 1.7;
                case "shy": return 1.5;
                case "startle": return 0.6;
                case "look": return 1.9;
                case "greet": return 1.5;
                case "lie": return 4.0;
                case "sleep": return 4.0;
                case "pace": return 2.6;
                case "peek": return 3.0;
                case "stroll": return 2.2;
                case "dash": return 1.4;
                case "follow": return 2.2;
                case "fireworks": return 1.4;
                case "hearts": return 1.2;
                case "sparkles": return 1.1;
                case "confetti": return 1.2;
                default: return 1.0;
            }
        }

        private void RefreshActionPreviewVisual()
        {
            if (actionPreviewHost == null) return;
            string role = cfg.GetString("", "role", "");
            if (role == actionPreviewRole && actionPreviewHost.Content != null) return;
            actionPreviewRole = role;
            actionPreviewDirector = null;
            actionPreviewHost.Content = null;
            try
            {
                PetPack previewPack = PetPack.TryLoad(cfg, role);
                if (previewPack != null)
                {
                    PackDirector director = new PackDirector(cfg, role, previewPack);
                    if (!string.IsNullOrEmpty(director.CurrentClip))
                    {
                        Viewbox box = new Viewbox();
                        box.Width = 220;
                        box.Height = 110;
                        box.Stretch = Stretch.Uniform;
                        box.Child = director.Visual;
                        actionPreviewHost.Content = box;
                        actionPreviewDirector = director;
                        return;
                    }
                }
            }
            catch
            {
            }
            try
            {
                string file = AppPaths.RoleThumbFile(role);
                if (!string.IsNullOrEmpty(file))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 220;
                    bitmap.EndInit();
                    Image image = new Image();
                    image.Source = bitmap;
                    image.Stretch = Stretch.Uniform;
                    image.Height = 110;
                    actionPreviewHost.Content = image;
                    return;
                }
            }
            catch
            {
            }
            actionPreviewHost.Content = Mascot.CreateThumbnail(110, 110);
        }

        private void RefreshActionControls()
        {
            if (!ui.ContainsKey("ActionGrid")) return;
            string role = cfg.GetString("", "role", "");
            actionPack = PetPack.TryLoad(cfg, role);
            ResetActionPreview();

            UniformGrid grid = (UniformGrid)ui["ActionGrid"];
            grid.Children.Clear();
            Button randomButton = new Button();
            randomButton.Content = "随机";
            randomButton.Style = (Style)root.Resources["Accent"];
            randomButton.ToolTip = "从当前宠物包的动作池随机预览一个";
            randomButton.Click += delegate { StartActionPreview("random", role); };
            grid.Children.Add(randomButton);

            List<string> keys = CurrentActionKeys();
            if (ui.ContainsKey("ActionListHint"))
            {
                ((TextBlock)ui["ActionListHint"]).Text = keys.Count == 0
                    ? "没有可预览动作"
                    : "动作列表 · " + keys.Count.ToString(CultureInfo.InvariantCulture) + " 个（可滚动）";
            }
            if (keys.Count == 0)
            {
                TextBlock hint = new TextBlock();
                hint.Text = "此宠物包未声明动作";
                hint.FontSize = 10;
                hint.Foreground = new SolidColorBrush(cMuted);
                hint.Margin = new Thickness(8, 4, 0, 4);
                hint.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(hint);
            }
            else
            {
                foreach (string key in keys)
                {
                    Button button = new Button();
                    string label = ActionLabel(key);
                    TextBlock text = new TextBlock();
                    text.Text = label;
                    text.TextTrimming = TextTrimming.CharacterEllipsis;
                    text.TextWrapping = TextWrapping.NoWrap;
                    text.HorizontalAlignment = HorizontalAlignment.Stretch;
                    text.TextAlignment = TextAlignment.Center;
                    button.Content = text;
                    button.Style = (Style)root.Resources["Btn"];
                    button.Tag = key;
                    bool selectedAction = string.Equals(actionPreviewKey, key, StringComparison.Ordinal);
                    button.BorderBrush = selectedAction ? Brush("#4B6BFB") : Brushes.Transparent;
                    button.BorderThickness = new Thickness(selectedAction ? 2 : 0);
                    string action = key;
                    button.ToolTip = "预览：" + label;
                    button.Click += delegate { StartActionPreview(action, role); };
                    grid.Children.Add(button);
                }
            }
            BuildActionPoolChips();
            RefreshActionPreviewVisual();
        }

        private void MarkSelectedActionButton(string key)
        {
            if (!ui.ContainsKey("ActionGrid")) return;
            foreach (object child in ((UniformGrid)ui["ActionGrid"]).Children)
            {
                Button button = child as Button;
                if (button == null || button.Tag == null) continue;
                bool selected = string.Equals(Convert.ToString(button.Tag), key, StringComparison.Ordinal);
                button.BorderBrush = selected ? Brush("#4B6BFB") : Brushes.Transparent;
                button.BorderThickness = new Thickness(selected ? 2 : 0);
            }
        }

        private List<string> CurrentActionKeys()
        {
            if (actionPack != null) return actionPack.ActionKeys();
            return new List<string>(PetWindow.ActionKeys);
        }

        private List<string> CurrentActionPool()
        {
            List<string> pool = new List<string>();
            if (actionPack == null)
            {
                pool.AddRange(cfg.GetStrings("actions", "pool"));
                return pool;
            }
            string role = cfg.GetString("", "role", "");
            Dictionary<string, object> section = cfg.GetObject("packPools", role);
            object stored = Json.Get(section, "pool");
            if (stored == null) return actionPack.DefaultActionPool();
            foreach (object item in Json.AsArray(stored))
            {
                string key = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(key) && actionPack.Action(key) != null && !pool.Contains(key)) pool.Add(key);
            }
            return pool;
        }

        private void SetCurrentActionPool(List<string> pool, string expectedRole)
        {
            string role = cfg.GetString("", "role", "");
            if (!string.Equals(role, expectedRole, StringComparison.Ordinal)) return;
            if (ActionPinSettings.Enabled(cfg, role)) return;
            PetPack pack = PetPack.TryLoad(cfg, role);
            if (pack != null)
            {
                Dictionary<string, object> section = cfg.GetObject("packPools", role);
                section["pool"] = pool.ToArray();
                cfg.Set("packPools", role, section);
            }
            else
            {
                cfg.SetAt("actions.pool", pool.ToArray());
            }
            cfg.Save();
            if (string.Equals(cfg.GetString("", "role", ""), expectedRole, StringComparison.Ordinal))
            {
                actionPack = pack;
                BuildActionPoolChips();
            }
        }

        private string ActionLabel(string key)
        {
            PackAction action = actionPack == null ? null : actionPack.Action(key);
            return action == null || string.IsNullOrEmpty(action.Label) ? Engine.ActionName(key) : action.Label;
        }

        private string ActionGroup(string key)
        {
            PackAction action = actionPack == null ? null : actionPack.Action(key);
            return action == null ? "" : action.Category;
        }

        /// <summary>动作池按类别分组：小动作 / 移动 / 特效；选中态用实心高亮。</summary>
        private void BuildActionPoolChips()
        {
            string role = cfg.GetString("", "role", "");
            WrapPanel idlePanel = (WrapPanel)ui["ActionPoolIdlePanel"];
            WrapPanel movePanel = (WrapPanel)ui["ActionPoolMovePanel"];
            WrapPanel fxPanel = (WrapPanel)ui["ActionPoolFxPanel"];
            idlePanel.Children.Clear();
            movePanel.Children.Clear();
            fxPanel.Children.Clear();
            List<string> pool = CurrentActionPool();
            List<string> keys = CurrentActionKeys();
            bool poolEditable = !ActionPinSettings.Enabled(cfg, role);
            Brush onBrush = Brush(cfg.GetStringAt("bubble.color1", "#4B6BFB"));
            Brush offBrush = new SolidColorBrush(cChip);
            int selected = 0;
            foreach (string key in keys)
            {
                string action = key;
                bool on = pool.Contains(key);
                if (on) selected++;
                WrapPanel target = idlePanel;
                string group = ActionGroup(key);
                if (string.Equals(group, "move", StringComparison.OrdinalIgnoreCase)
                    || (actionPack == null && Array.IndexOf(MoveActionKeys, key) >= 0)) target = movePanel;
                else if (string.Equals(group, "fx", StringComparison.OrdinalIgnoreCase)
                    || (actionPack == null && Array.IndexOf(FxActionKeys, key) >= 0)) target = fxPanel;
                Button chip = new Button();
                chip.Content = (on ? "✓ " : "") + ActionLabel(key);
                chip.FontSize = 10.5;
                chip.Height = 26;
                chip.Padding = new Thickness(9, 0, 9, 0);
                chip.Margin = new Thickness(0, 0, 6, 6);
                chip.Cursor = Cursors.Hand;
                chip.Background = on ? onBrush : offBrush;
                chip.Foreground = on ? Brushes.White : new SolidColorBrush(cFg2);
                chip.IsEnabled = poolEditable;
                chip.ToolTip = on ? "自动播放中（点击移出）" : "未参与自动播放（点击加入）";
                chip.Template = (ControlTemplate)XamlReader.Parse(
                    "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>"
                    + "<Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'>"
                    + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>"
                    + "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'>"
                    + "<Setter TargetName='b' Property='Opacity' Value='0.85'/>"
                    + "</Trigger></ControlTemplate.Triggers></ControlTemplate>");
                chip.Click += delegate
                {
                    List<string> current = CurrentActionPool();
                    if (current.Contains(action)) current.Remove(action);
                    else current.Add(action);
                    SetCurrentActionPool(current, role);
                };
                target.Children.Add(chip);
            }
            ((TextBlock)ui["ActionPoolCount"]).Text = "已选 " + selected.ToString(CultureInfo.InvariantCulture)
                + " / " + keys.Count.ToString(CultureInfo.InvariantCulture);
        }

        private string CurrentSinglePngRole()
        {
            string role = cfg.GetString("", "role", "");
            return RigSettings.IsSinglePngRole(role) ? role : "";
        }

        /// <summary>
        /// The rig controls are scoped to the selected PNG only. Hide the whole card
        /// when the current role is a GIF, frame folder, imported pack or mascot.
        /// </summary>
        private void RefreshRigScope()
        {
            if (!ui.ContainsKey("RigImageCard") || !ui.ContainsKey("RigScopeHint")) return;
            string role = CurrentSinglePngRole();
            bool available = !string.IsNullOrEmpty(role);
            ui["RigImageCard"].Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            ui["RigScopeHint"].Visibility = available ? Visibility.Collapsed : Visibility.Visible;
            if (!available) return;

            ((TextBlock)ui["RigRoleName"]).Text = "仅保存到：" + System.IO.Path.GetFileNameWithoutExtension(role);
            ((CheckBox)ui["MeshRigChk"]).IsChecked = RigSettings.GetFlag(cfg, role, "mesh", false);
            double intensity = RigSettings.GetNumber(cfg, role, "intensity", 1.0);
            ((Slider)ui["RigIntensitySlider"]).Value = Math.Max(0, Math.Min(2, intensity));
            ((TextBlock)ui["RigIntensityVal"]).Text = intensity.ToString("0.0", CultureInfo.InvariantCulture);
            double faceY = RigSettings.GetNumber(cfg, role, "faceY", 0.25);
            ((Slider)ui["RigFaceYSlider"]).Value = Math.Max(0.10, Math.Min(0.50, faceY));
            ((TextBlock)ui["RigFaceYVal"]).Text = faceY.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void BuildRoleButtons()
        {
            WrapPanel panel = (WrapPanel)ui["RolePanel"];
            panel.Children.Clear();   // 刷新前先清空，避免重复叠加
            string selected = cfg.GetString("", "role", "");
            List<string> imported = new List<string>();
            List<string> plain = new List<string>();
            List<string> files = AppPaths.RoleFiles();
            foreach (string file in files)
            {
                if (IsImportedPackRole(file)) imported.Add(file);
                else plain.Add(file);
            }
            TextBlock summary = ui.ContainsKey("RoleLibrarySummary")
                ? ui["RoleLibrarySummary"] as TextBlock : null;
            if (summary != null)
            {
                summary.Text = files.Count == 0
                    ? "素材库为空"
                    : "素材库共 " + files.Count.ToString(CultureInfo.InvariantCulture)
                        + " 项 · 导入包 " + imported.Count.ToString(CultureInfo.InvariantCulture)
                        + " · 单图/动图 " + plain.Count.ToString(CultureInfo.InvariantCulture);
                summary.ToolTip = AppPaths.RolesDir;
            }

            AddRoleSectionHeader(panel, "内置角色");
            AddRoleButton(panel, "", "内置小鲸鱼", selected, null);
            if (imported.Count > 0)
            {
                AddRoleSectionHeader(panel, "导入的宠物包 · " + imported.Count.ToString(CultureInfo.InvariantCulture));
                foreach (string file in imported)
                {
                    PetPack pack = PetPack.TryLoad(cfg, file);
                    string label = pack == null || string.IsNullOrEmpty(pack.Name)
                        ? "导入宠物" : PublicPackName(pack.Name);
                    int actionCount = pack == null ? 0 : pack.ActionKeys().Count;
                    string source = RoleImportSource(file);
                    string detail = "导入包";
                    if (!string.IsNullOrEmpty(source)) detail += " · " + source;
                    if (actionCount > 0) detail += " · " + actionCount.ToString(CultureInfo.InvariantCulture) + " 动作";
                    AddRoleButton(panel, file, label, selected, file, detail, true);
                }
            }
            if (plain.Count > 0)
            {
                AddRoleSectionHeader(panel, "单图 / 动图素材 · " + plain.Count.ToString(CultureInfo.InvariantCulture));
                foreach (string file in plain)
                {
                    string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    string label = System.IO.Path.GetFileNameWithoutExtension(file);
                    string detail = Directory.Exists(System.IO.Path.Combine(AppPaths.RolesDir, file))
                        ? "图片动图" : (extension == ".gif" ? "GIF 动图" : "单图");
                    AddRoleButton(panel, file, label, selected, file, detail, false);
                }
            }
            if (files.Count == 0)
            {
                TextBlock hint = new TextBlock();
                hint.Text = "把角色 PNG 放进 roles\\ 目录即可在这里选用";
                hint.FontSize = 10;
                hint.Foreground = new SolidColorBrush(cMuted);
                hint.TextWrapping = TextWrapping.Wrap;
                hint.Margin = new Thickness(2, 6, 0, 0);
                panel.Children.Add(hint);
            }
        }

        private static bool IsImportedPackRole(string role)
        {
            try
            {
                string path = System.IO.Path.Combine(AppPaths.RolesDir, role);
                return Directory.Exists(path)
                    && (File.Exists(System.IO.Path.Combine(path, "pet.json"))
                        || File.Exists(System.IO.Path.Combine(path, "import-source.txt"))
                        || File.Exists(System.IO.Path.Combine(path, "imported-pack.txt")));
            }
            catch
            {
                return false;
            }
        }

        private static string RoleImportSource(string role)
        {
            try
            {
                string path = System.IO.Path.Combine(AppPaths.RolesDir, role, "import-source.txt");
                if (!File.Exists(path)) return "";
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("source=", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring("source=".Length).Trim();
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        /// <summary>Role names are user-facing; drop old parenthetical import suffixes.</summary>
        private static string PublicPackName(string name)
        {
            string label = (name ?? "").Trim();
            int open = label.LastIndexOf('（');
            if (open > 0 && label.EndsWith("导入）", StringComparison.Ordinal))
            {
                label = label.Substring(0, open).Trim();
            }
            return string.IsNullOrEmpty(label) ? "导入宠物" : label;
        }

        private void AddRoleSectionHeader(WrapPanel panel, string text)
        {
            TextBlock header = new TextBlock();
            header.Text = text;
            header.Width = 500;
            header.FontSize = 10;
            header.FontWeight = FontWeights.SemiBold;
            header.Foreground = new SolidColorBrush(cFg2);
            header.Margin = new Thickness(1, 7, 0, 5);
            panel.Children.Add(header);
        }

        private void AddRoleButton(WrapPanel panel, string tag, string label, string selected, string thumbFile)
        {
            AddRoleButton(panel, tag, label, selected, thumbFile, "", false);
        }

        private void AddRoleButton(WrapPanel panel, string tag, string label, string selected,
            string thumbFile, string detail, bool imported)
        {
            Button button = new Button();
            button.Style = (Style)root.Resources["Btn"];
            button.Height = 84;
            button.MinWidth = 84;
            button.MaxWidth = 122;
            button.Padding = new Thickness(4);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
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
                    // 文件夹角色（逐帧动画）没有单张图，取第一帧当缩略图。
                    string file = AppPaths.RoleThumbFile(thumbFile);
                    if (string.IsNullOrEmpty(file)) throw new System.IO.FileNotFoundException(thumbFile);
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file);
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
            text.MaxWidth = 108;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0, 3, 0, 0);
            text.ToolTip = label;
            stack.Children.Add(text);
            if (!string.IsNullOrEmpty(detail))
            {
                TextBlock detailText = new TextBlock();
                detailText.Text = detail;
                detailText.FontSize = 8.5;
                detailText.MaxWidth = 112;
                detailText.TextTrimming = TextTrimming.CharacterEllipsis;
                detailText.HorizontalAlignment = HorizontalAlignment.Center;
                detailText.Margin = new Thickness(0, 1, 0, 0);
                detailText.Foreground = imported ? Brush("#4B6BFB") : new SolidColorBrush(cMuted);
                detailText.ToolTip = detail;
                stack.Children.Add(detailText);
            }
            button.Content = stack;
            button.ToolTip = string.IsNullOrEmpty(detail) ? label : label + "\n" + detail;
            string chosen = tag;
            button.Click += delegate
            {
                cfg.Set("", "role", chosen);
                cfg.SetAt("roles.random", false);   // an explicit pick wins over random looks
                foreach (PetWindow pet in engine.Pets) pet.ApplyRole(chosen);
                cfg.Save();
                RefreshActionControls();
                Update();
            };
            button.BorderBrush = Convert.ToString(button.Tag) == selected ? Brush("#4B6BFB") : Brushes.Transparent;
            button.BorderThickness = new Thickness(Convert.ToString(button.Tag) == selected ? 2 : 0);
            if (string.IsNullOrEmpty(thumbFile) || !roleEditMode)
            {
                panel.Children.Add(button);
                return;
            }
            // 自定义素材右上角挂一个删除按钮：删完立刻刷新列表，不用重启面板。
            Grid holder = new Grid();
            holder.Margin = new Thickness(0, 0, 7, 7);
            button.Margin = new Thickness(0);
            holder.Children.Add(button);
            Button del = new Button();
            del.Width = 16;
            del.Height = 16;
            del.Padding = new Thickness(0);
            del.BorderThickness = new Thickness(0);
            del.Cursor = Cursors.Hand;
            del.FontSize = 10;
            del.Foreground = Brushes.White;
            del.Content = "×";
            del.HorizontalAlignment = HorizontalAlignment.Right;
            del.VerticalAlignment = VerticalAlignment.Top;
            del.Margin = new Thickness(0, 2, 2, 0);
            del.ToolTip = "删除这个素材（会从 roles 目录移除，无法恢复）";
            del.Template = (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>"
                + "<Border x:Name='b' Background='#E5484D' CornerRadius='8'>"
                + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>"
                + "</Border>"
                + "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'>"
                + "<Setter TargetName='b' Property='Opacity' Value='0.82'/>"
                + "</Trigger></ControlTemplate.Triggers></ControlTemplate>");
            AutomationProperties.SetName(del, "删除 " + label);
            string roleTag = tag;
            del.Click += delegate { DeleteRoleAsset(roleTag); };
            holder.Children.Add(del);
            panel.Children.Add(holder);
        }

        /// <summary>切换「编辑」模式时同步按钮文案与添加按钮的显示。</summary>
        private void UpdateRoleEditState()
        {
            if (!ui.ContainsKey("RoleEditBtn")) return;
            Button edit = ui["RoleEditBtn"] as Button;
            if (edit != null)
            {
                edit.Content = roleEditMode ? "完成" : "编辑";
                edit.BorderThickness = new Thickness(roleEditMode ? 2 : 0);
                edit.BorderBrush = roleEditMode ? Brush("#4B6BFB") : Brushes.Transparent;
            }
        }

        /// <summary>与面板同色的确认框（系统 MessageBox 和整体观感不搭）。</summary>
        private bool ConfirmDialog(string headText, string bodyText, string okText)
        {
            Window dialog = new Window();
            dialog.WindowStyle = WindowStyle.None;
            dialog.AllowsTransparency = true;
            dialog.Background = Brushes.Transparent;
            dialog.ShowInTaskbar = false;
            dialog.Topmost = true;
            dialog.SizeToContent = SizeToContent.WidthAndHeight;
            dialog.ResizeMode = ResizeMode.NoResize;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.FontFamily = root.FontFamily;
            Border card = new Border();
            card.CornerRadius = new CornerRadius(12);
            card.Width = 320;
            card.Padding = new Thickness(18, 14, 18, 12);
            card.Background = new SolidColorBrush(cCard);
            card.BorderBrush = new SolidColorBrush(cBorder);
            card.BorderThickness = new Thickness(1);
            StackPanel stack = new StackPanel();
            TextBlock head = new TextBlock();
            head.Text = headText;
            head.FontSize = 13;
            head.FontWeight = FontWeights.SemiBold;
            head.Foreground = new SolidColorBrush(cFg);
            TextBlock body = new TextBlock();
            body.Text = bodyText;
            body.FontSize = 11.5;
            body.Foreground = new SolidColorBrush(cFg2);
            body.TextWrapping = TextWrapping.Wrap;
            body.Margin = new Thickness(0, 8, 0, 12);
            DockPanel row = new DockPanel();
            row.HorizontalAlignment = HorizontalAlignment.Right;
            Button cancel = new Button();
            cancel.Content = "取消";
            cancel.Width = 64;
            cancel.Style = (Style)root.Resources["Btn"];
            Button ok = new Button();
            ok.Content = okText;
            ok.Width = 64;
            ok.Style = (Style)root.Resources["Btn"];
            ok.Foreground = new SolidColorBrush(cErr);
            bool confirmed = false;
            cancel.Click += delegate { dialog.Close(); };
            ok.Click += delegate { confirmed = true; dialog.Close(); };
            row.Children.Add(cancel);
            row.Children.Add(ok);
            stack.Children.Add(head);
            stack.Children.Add(body);
            stack.Children.Add(row);
            card.Child = stack;
            dialog.Content = card;
            dialog.Owner = root;
            dialog.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) dialog.Close();
                else if (e.Key == Key.Enter) { confirmed = true; dialog.Close(); }
            };
            dialog.ShowDialog();
            return confirmed;
        }

        /// <summary>删除 roles 目录里的一个角色素材（文件或逐帧目录），并刷新外观列表。</summary>
        private void DeleteRoleAsset(string role)
        {
            if (string.IsNullOrEmpty(role)) return;
            if (!ConfirmDialog("删除角色素材", "「" + role + "」会从 roles 目录彻底删除，无法恢复。", "删除")) return;
            try
            {
                string rootDir = System.IO.Path.GetFullPath(AppPaths.RolesDir);
                string guard = rootDir.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
                string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(rootDir, role));
                if (!target.StartsWith(guard, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("素材路径不在 roles 目录内");
                }
                if (System.IO.Directory.Exists(target)) System.IO.Directory.Delete(target, true);
                else if (System.IO.File.Exists(target)) System.IO.File.Delete(target);
            }
            catch (Exception ex)
            {
                MessageBox.Show(root, "删除失败：" + ex.Message, "删除角色素材", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (cfg.GetString("", "role", "") == role)
            {
                cfg.Set("", "role", "");
                cfg.Save();
                foreach (PetWindow pet in engine.Pets) pet.ApplyRole("");
            }
            BuildRoleButtons();
            Update();
        }

        // ------------------------------------------------------------ 数据源
        private void BuildProviderRows()
        {
            StackPanel list = (StackPanel)ui["ProviderList"];
            StackPanel keyList = (StackPanel)ui["ProviderKeyList"];
            WrapPanel unsupported = (WrapPanel)ui["UnsupportedPanel"];
            list.Children.Clear();
            keyList.Children.Clear();
            unsupported.Children.Clear();
            keyBoxes.Clear();
            platformTokenBoxes.Clear();
            foreach (ProviderDef def in Providers.Effective(cfg))
            {
                if (!def.HasBalance)
                {
                    Border chip = new Border();
                    chip.CornerRadius = new CornerRadius(8);
                    chip.Background = new SolidColorBrush(cChip);
                    chip.Padding = new Thickness(8, 3, 8, 3);
                    chip.Margin = new Thickness(0, 0, 6, 6);
                    chip.ToolTip = def.Note;
                    TextBlock chipText = new TextBlock();
                    chipText.Text = def.Name;
                    chipText.FontSize = 10.5;
                    chipText.Foreground = new SolidColorBrush(cMuted);
                    chip.Child = chipText;
                    unsupported.Children.Add(chip);
                    continue;
                }
                list.Children.Add(BuildProviderRow(def));
                keyList.Children.Add(BuildProviderCredentialRow(def));
            }
        }

        private FrameworkElement BuildProviderRow(ProviderDef def)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(11);
            row.Background = new SolidColorBrush(cRow);
            row.BorderBrush = new SolidColorBrush(cBorder);
            row.BorderThickness = new Thickness(1);
            row.Padding = new Thickness(12, 10, 12, 11);
            row.Margin = new Thickness(0, 0, 0, 8);
            row.ToolTip = string.IsNullOrEmpty(def.Note) ? def.Name : def.Note;
            StackPanel stack = new StackPanel();

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border avatar = new Border();
            avatar.Width = 30;
            avatar.Height = 30;
            avatar.CornerRadius = new CornerRadius(9);
            avatar.Background = new SolidColorBrush(cChip);
            avatar.Margin = new Thickness(0, 0, 9, 0);
            TextBlock avatarText = new TextBlock();
            avatarText.Text = string.IsNullOrEmpty(def.Name)
                ? "?" : def.Name.Substring(0, 1);
            avatarText.FontSize = 12;
            avatarText.FontWeight = FontWeights.Bold;
            avatarText.Foreground = new SolidColorBrush(cFg2);
            avatarText.HorizontalAlignment = HorizontalAlignment.Center;
            avatarText.VerticalAlignment = VerticalAlignment.Center;
            avatar.Child = avatarText;
            head.Children.Add(avatar);
            StackPanel identity = new StackPanel();
            identity.VerticalAlignment = VerticalAlignment.Center;
            TextBlock name = new TextBlock();
            name.Text = def.Name;
            name.FontSize = 11.8;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(cFg);
            identity.Children.Add(name);
            TextBlock description = new TextBlock();
            description.Text = string.IsNullOrEmpty(def.Note) ? "余额接口" : def.Note;
            description.FontSize = 9.2;
            description.Foreground = new SolidColorBrush(cMuted);
            description.TextTrimming = TextTrimming.CharacterEllipsis;
            description.Margin = new Thickness(0, 2, 8, 0);
            identity.Children.Add(description);
            Grid.SetColumn(identity, 1);
            head.Children.Add(identity);

            Border statusPill = new Border();
            statusPill.CornerRadius = new CornerRadius(7);
            statusPill.Background = new SolidColorBrush(cChip);
            statusPill.Padding = new Thickness(7, 2, 7, 2);
            statusPill.Margin = new Thickness(6, 0, 8, 0);
            TextBlock status = new TextBlock();
            status.FontSize = 9.6;
            status.Foreground = new SolidColorBrush(cMuted);
            status.VerticalAlignment = VerticalAlignment.Center;
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            status.MaxWidth = 126;
            statusPill.Child = status;
            Grid.SetColumn(statusPill, 2);
            head.Children.Add(statusPill);
            CheckBox toggle = new CheckBox();
            toggle.Style = (Style)root.Resources["Switch"];
            toggle.Content = "";
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
            Grid.SetColumn(toggle, 3);
            toggle.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(toggle);
            stack.Children.Add(head);
            statusById[def.Id] = status;

            row.Child = stack;
            return row;
        }

        private FrameworkElement BuildProviderCredentialRow(ProviderDef def)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(10);
            row.Background = new SolidColorBrush(cField);
            row.BorderBrush = new SolidColorBrush(cBorder);
            row.BorderThickness = new Thickness(1);
            row.Padding = new Thickness(11, 8, 11, 9);
            row.Margin = new Thickness(0, 0, 0, 7);
            StackPanel stack = new StackPanel();

            DockPanel head = new DockPanel();
            TextBlock name = new TextBlock();
            name.Text = def.Name + " · 接入凭据";
            name.FontSize = 10.8;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(cFg2);
            head.Children.Add(name);
            TextBlock source = new TextBlock();
            source.Text = cfg.GetStringAt("providers." + def.Id + ".apiKey", "") != ""
                ? "已配置" : "未配置";
            source.FontSize = 9.5;
            source.Foreground = new SolidColorBrush(
                cfg.GetStringAt("providers." + def.Id + ".apiKey", "") != "" ? cOk : cMuted);
            source.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(source, Dock.Right);
            head.Children.Add(source);
            stack.Children.Add(head);

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
                authHint.Text = "URL 支持 {key} 占位；路径可留空";
                authHint.FontSize = 9.5;
                authHint.Foreground = new SolidColorBrush(cMuted);
                authHint.VerticalAlignment = VerticalAlignment.Center;
                authRow.Children.Add(authHint);
                stack.Children.Add(authRow);
            }

            DockPanel keyRow = new DockPanel();
            keyRow.Margin = new Thickness(0, 7, 0, 0);
            TextBlock keyLabel = new TextBlock();
            keyLabel.Text = "API Key";
            keyLabel.FontSize = 9.5;
            keyLabel.Foreground = new SolidColorBrush(cMuted);
            keyLabel.Width = 54;
            keyLabel.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(keyLabel, Dock.Left);
            keyRow.Children.Add(keyLabel);
            Button apply = new Button();
            apply.Style = (Style)root.Resources["Mini"];
            apply.Content = "应用";
            apply.Width = 46;
            apply.Padding = new Thickness(0);
            DockPanel.SetDock(apply, Dock.Right);
            apply.Click += delegate { ApplyKey(def.Id); };
            Button clear = new Button();
            clear.Style = (Style)root.Resources["Mini"];
            clear.Content = "清除";
            clear.Width = 46;
            clear.Padding = new Thickness(0);
            clear.Margin = new Thickness(0, 0, 6, 0);
            DockPanel.SetDock(clear, Dock.Right);
            clear.Click += delegate { ClearKey(def.Id); };
            keyRow.Children.Add(apply);
            keyRow.Children.Add(clear);
            PasswordBox box = new PasswordBox();
            box.Style = (Style)root.Resources["KeyBox"];
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) ApplyKey(def.Id);
            };
            keyBoxes[def.Id] = box;
            keyRow.Children.Add(BuildSecretInput(box,
                cfg.GetStringAt("providers." + def.Id + ".apiKey", ""), "输入 API Key"));
            stack.Children.Add(keyRow);
            if (def.Id == "deepseek") stack.Children.Add(BuildDeepSeekUsageRows());

            row.Child = stack;
            return row;
        }

        private FrameworkElement BuildSecretInput(PasswordBox box, string storedValue, string placeholder)
        {
            Grid host = new Grid();
            host.Children.Add(box);
            string masked = MaskSecret(storedValue);
            bool configured = masked.Length > 0;
            TextBlock preview = new TextBlock();
            preview.Text = configured ? masked : placeholder;
            preview.FontFamily = new FontFamily("Consolas, Microsoft YaHei UI");
            preview.FontSize = 10.5;
            preview.Foreground = new SolidColorBrush(configured ? cFg2 : cMuted);
            preview.VerticalAlignment = VerticalAlignment.Center;
            preview.Margin = new Thickness(9, 0, 8, 0);
            preview.TextTrimming = TextTrimming.CharacterEllipsis;
            preview.IsHitTestVisible = false;
            host.Children.Add(preview);
            box.PasswordChanged += delegate
            {
                preview.Visibility = box.Password.Length > 0
                    ? Visibility.Collapsed : Visibility.Visible;
            };
            return host;
        }

        private static string MaskSecret(string storedValue)
        {
            string raw = Secret.Unprotect(storedValue ?? "").Trim();
            if (raw.Length == 0) return Secret.IsProtected(storedValue) ? "••••••••" : "";
            if (raw.Length <= 12)
                return "••••••••" + raw.Substring(Math.Max(0, raw.Length - 4));
            return raw.Substring(0, 3) + "••••••••" + raw.Substring(raw.Length - 4);
        }

        private FrameworkElement BuildDeepSeekUsageRows()
        {
            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(0, 8, 0, 0);

            TextBlock label = new TextBlock();
            label.Text = "平台 Token（可选，用于按 Key 精确成本）";
            label.FontSize = 10;
            label.Foreground = new SolidColorBrush(cMuted);
            label.Margin = new Thickness(0, 0, 0, 4);
            label.ToolTip = "在 platform.deepseek.com 登录后，从浏览器 localStorage.userToken 的 value 复制。"
                + "令牌只保存在本机 pet-config.json，并使用 Windows DPAPI 加密。";
            panel.Children.Add(label);

            DockPanel row = new DockPanel();
            Button apply = new Button();
            apply.Style = (Style)root.Resources["Mini"];
            apply.Content = "应用";
            apply.Width = 46;
            apply.Padding = new Thickness(0);
            DockPanel.SetDock(apply, Dock.Right);
            apply.Click += delegate { ApplyPlatformToken(); };
            Button clear = new Button();
            clear.Style = (Style)root.Resources["Mini"];
            clear.Content = "清除";
            clear.Width = 46;
            clear.Padding = new Thickness(0);
            clear.Margin = new Thickness(0, 0, 6, 0);
            DockPanel.SetDock(clear, Dock.Right);
            clear.Click += delegate { ClearPlatformToken(); };
            row.Children.Add(apply);
            row.Children.Add(clear);

            PasswordBox box = new PasswordBox();
            box.Style = (Style)root.Resources["KeyBox"];
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) ApplyPlatformToken();
            };
            platformTokenBoxes["deepseek"] = box;
            row.Children.Add(BuildSecretInput(box,
                cfg.GetStringAt("providers.deepseek.platformToken", ""), "输入平台 Token"));
            panel.Children.Add(row);

            CheckBox showKey = new CheckBox();
            showKey.Style = (Style)root.Resources["Switch"];
            showKey.Content = "显示当前 Key 消耗";
            showKey.FontSize = 10.5;
            showKey.Margin = new Thickness(0, 7, 0, 0);
            showKey.IsChecked = cfg.GetFlagAt("providers.deepseek.showKeyUsage", true);
            showKey.Click += delegate
            {
                cfg.SetAt("providers.deepseek.showKeyUsage", showKey.IsChecked == true);
                cfg.Save();
            };
            panel.Children.Add(showKey);

            TextBlock hint = new TextBlock();
            hint.Text = "未填写平台 Token 时，仍显示本机余额变化统计；填写后才显示平台精确成本。";
            hint.FontSize = 9.5;
            hint.Foreground = new SolidColorBrush(cMuted);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 5, 0, 0);
            panel.Children.Add(hint);
            return panel;
        }

        private FrameworkElement FieldRow(string label, string path, double boxWidth, double labelWidth)
        {
            DockPanel row = new DockPanel();
            row.Margin = new Thickness(0, 6, 0, 0);
            TextBlock prefix = new TextBlock();
            prefix.Text = label;
            prefix.FontSize = 11;
            prefix.Foreground = new SolidColorBrush(cMuted);
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
            BuildProviderRows();
            engine.Data.Refresh();
            Update();
        }

        private void ClearKey(string id)
        {
            cfg.SetAt("providers." + id + ".apiKey", "");
            cfg.Save();
            BuildProviderRows();
            engine.Data.Refresh();
            Update();
        }

        private void ApplyPlatformToken()
        {
            PasswordBox box;
            if (!platformTokenBoxes.TryGetValue("deepseek", out box)) return;
            string text = box.Password.Trim();
            if (text.Length == 0) return;
            cfg.SetAt("providers.deepseek.platformToken", Secret.Protect(text));
            box.Password = "";
            cfg.Save();
            BuildProviderRows();
            engine.Data.Refresh();
            Update();
        }

        private void ClearPlatformToken()
        {
            cfg.SetAt("providers.deepseek.platformToken", "");
            cfg.Save();
            BuildProviderRows();
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
                    colour = string.Format("#{0:X2}{1:X2}{2:X2}", cOk.R, cOk.G, cOk.B);
                }
                else
                {
                    text = state.Error;
                    colour = string.Format("#{0:X2}{1:X2}{2:X2}", cErr.R, cErr.G, cErr.B);
                }
                if (text != null && text.Length > 26) text = text.Substring(0, 26) + "…";
                pair.Value.Text = text;
                pair.Value.Foreground = Brush(colour);
                string tooltip = state != null && !string.IsNullOrEmpty(state.ErrorDetail) ? state.ErrorDetail : "";
                if (state != null && state.UsageExact)
                {
                    tooltip += (tooltip.Length == 0 ? "" : "\n")
                        + "平台精确成本 · 今日 " + Fmt.Money(state.TodayTotalUsage, state.Currency);
                    if (state.TodayKeyUsage.HasValue)
                    {
                        tooltip += " · 当前 Key " + Fmt.Money(state.TodayKeyUsage, state.Currency);
                    }
                    if (state.LifetimeUsage.HasValue)
                    {
                        tooltip += " · 累计 " + Fmt.Money(state.LifetimeUsage, state.Currency);
                    }
                }
                if (state != null && !string.IsNullOrEmpty(state.UsageError))
                {
                    tooltip += (tooltip.Length == 0 ? "" : "\n") + state.UsageError;
                }
                pair.Value.ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
            }
        }

        // ------------------------------------------------------------ 提醒（按平台）
        private string alertRowKey = "";

        /// <summary>
        /// 提醒页按平台生成一行「余额低于 / 今日预算」：各家的币种与量级差太多，一个全局
        /// 数字没有意义。留空 = 该平台不提醒；只在启用的平台集合变化时重建（别打断打字）。
        /// </summary>
        private void BuildAlertRows()
        {
            StackPanel providerList = (StackPanel)ui["ProviderAlertList"];
            StackPanel planList = (StackPanel)ui["PlanAlertList"];
            StringBuilder key = new StringBuilder();
            foreach (ProviderDef def in Providers.Effective(cfg))
            {
                if (cfg.GetFlagAt("providers." + def.Id + ".on", false)) key.Append(def.Id).Append(',');
            }
            key.Append('|');
            foreach (PlanDef plan in Plans.Catalog())
            {
                if (cfg.GetFlagAt("plans." + plan.Id + ".on", false)) key.Append(plan.Id).Append(',');
            }
            string signature = key.ToString();
            if (signature == alertRowKey
                && providerList.Children.Count > 0
                && planList.Children.Count > 0) return;
            alertRowKey = signature;
            providerList.Children.Clear();
            planList.Children.Clear();
            List<UIElement> providerRows = new List<UIElement>();
            foreach (ProviderDef def in Providers.Effective(cfg))
            {
                if (!cfg.GetFlagAt("providers." + def.Id + ".on", false)) continue;
                if (!def.HasBalance) continue;   // 没有公开余额接口的平台没法做余额预警
                providerRows.Add(BuildAlertRow(def));
            }
            List<UIElement> planRows = new List<UIElement>();
            foreach (PlanDef plan in Plans.Catalog())
            {
                if (!cfg.GetFlagAt("plans." + plan.Id + ".on", false)) continue;
                planRows.Add(BuildPlanAlertRow(plan));
            }
            if (providerRows.Count > 0)
            {
                foreach (UIElement row in providerRows) providerList.Children.Add(row);
            }
            else
            {
                providerList.Children.Add(PlanStatusLine("启用余额接口后，这里会显示各平台的阈值设置。"));
            }
            if (planRows.Count > 0)
            {
                foreach (UIElement row in planRows) planList.Children.Add(row);
            }
            else
            {
                planList.Children.Add(PlanStatusLine("启用套餐后，这里会显示各套餐的阈值设置。"));
            }
        }

        private FrameworkElement BuildAlertRow(ProviderDef def)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Background = new SolidColorBrush(cRow);
            row.Padding = new Thickness(10, 6, 10, 6);
            row.Margin = new Thickness(0, 0, 0, 6);
            DockPanel line = new DockPanel();

            TextBlock name = new TextBlock();
            name.Text = def.Name;
            name.FontSize = 11;
            name.Foreground = new SolidColorBrush(cFg2);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Width = 118;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.ToolTip = def.Name;
            line.Children.Add(name);

            line.Children.Add(AlertCaption("余额低于"));
            TextBox belowBox = AlertBox("alerts." + def.Id + ".below");
            line.Children.Add(belowBox);
            line.Children.Add(AlertCaption("今日预算"));
            TextBox budgetBox = AlertBox("alerts." + def.Id + ".budget");
            line.Children.Add(budgetBox);
            row.Child = line;
            return row;
        }

        /// <summary>套餐额度提醒：每个启用的套餐一行，「5 小时 ≥ x%」「7 天（含每周/30 天）≥ y%」。</summary>
        private FrameworkElement BuildPlanAlertRow(PlanDef plan)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Background = new SolidColorBrush(cRow);
            row.Padding = new Thickness(10, 6, 10, 6);
            row.Margin = new Thickness(0, 0, 0, 6);
            DockPanel line = new DockPanel();

            TextBlock name = new TextBlock();
            name.Text = plan.Name;
            name.FontSize = 11;
            name.Foreground = new SolidColorBrush(cFg2);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Width = 118;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.ToolTip = plan.Name;
            line.Children.Add(name);

            line.Children.Add(AlertCaption("5 小时 ≥"));
            line.Children.Add(AlertBox("alerts." + plan.Id + ".fiveHour"));
            line.Children.Add(AlertCaption("%"));
            line.Children.Add(AlertCaption("7 天 ≥"));
            line.Children.Add(AlertBox("alerts." + plan.Id + ".sevenDay"));
            line.Children.Add(AlertCaption("%"));
            row.Child = line;
            return row;
        }

        private TextBlock AlertCaption(string text)
        {
            TextBlock caption = new TextBlock();
            caption.Text = text;
            caption.FontSize = 10;
            caption.Foreground = new SolidColorBrush(cMuted);
            caption.VerticalAlignment = VerticalAlignment.Center;
            caption.Margin = new Thickness(6, 0, 6, 0);
            return caption;
        }

        private TextBox AlertBox(string path)
        {
            TextBox box = new TextBox();
            box.Style = (Style)root.Resources["TxtBox"];
            box.Width = 58;
            box.ToolTip = "留空 = 该平台不提醒";
            double value = cfg.GetNumberAt(path, 0);
            box.Text = value > 0 ? value.ToString("0.##", CultureInfo.InvariantCulture) : "";
            box.LostFocus += delegate { SaveAlertBox(box, path); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) SaveAlertBox(box, path);
            };
            return box;
        }

        private void SaveAlertBox(TextBox box, string path)
        {
            double value;
            if (!double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0) value = 0;
            // 套餐窗口阈值是百分比：100% 封顶（到顶=用满，再高没意义）。
            if (value > 100 && (path.EndsWith(".fiveHour", StringComparison.Ordinal) || path.EndsWith(".sevenDay", StringComparison.Ordinal)))
            {
                value = 100;
            }
            cfg.SetAt(path, value);
            box.Text = value > 0 ? value.ToString("0.##", CultureInfo.InvariantCulture) : "";
            cfg.Save();
            engine.Data.Refresh();
        }

        // ------------------------------------------------------------ 套餐用量
        private readonly Dictionary<string, TextBlock> planStatus = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, PasswordBox> planBoxes = new Dictionary<string, PasswordBox>();
        private readonly Dictionary<string, StackPanel> planWindowHosts = new Dictionary<string, StackPanel>();

        private void BuildPlanRows()
        {
            StackPanel list = (StackPanel)ui["PlanList"];
            StackPanel keyList = (StackPanel)ui["PlanKeyList"];
            list.Children.Clear();
            keyList.Children.Clear();
            planStatus.Clear();
            planBoxes.Clear();
            planWindowHosts.Clear();
            foreach (PlanDef def in Plans.Catalog())
            {
                Border row = new Border();
                row.CornerRadius = new CornerRadius(11);
                row.Background = new SolidColorBrush(cRow);
                row.BorderBrush = new SolidColorBrush(cBorder);
                row.BorderThickness = new Thickness(1);
                row.Padding = new Thickness(12, 10, 12, 11);
                row.Margin = new Thickness(0, 0, 0, 8);
                row.ToolTip = string.IsNullOrEmpty(def.Note) ? def.Name : def.Note;
                StackPanel stack = new StackPanel();

                Grid head = new Grid();
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Border avatar = new Border();
                avatar.Width = 30;
                avatar.Height = 30;
                avatar.CornerRadius = new CornerRadius(9);
                avatar.Background = new SolidColorBrush(cChip);
                avatar.Margin = new Thickness(0, 0, 9, 0);
                TextBlock avatarText = new TextBlock();
                avatarText.Text = string.IsNullOrEmpty(def.Name)
                    ? "?" : def.Name.Substring(0, 1);
                avatarText.FontSize = 12;
                avatarText.FontWeight = FontWeights.Bold;
                avatarText.Foreground = new SolidColorBrush(cFg2);
                avatarText.HorizontalAlignment = HorizontalAlignment.Center;
                avatarText.VerticalAlignment = VerticalAlignment.Center;
                avatar.Child = avatarText;
                head.Children.Add(avatar);
                StackPanel identity = new StackPanel();
                identity.VerticalAlignment = VerticalAlignment.Center;
                TextBlock name = new TextBlock();
                name.Text = def.Name;
                name.FontSize = 11.8;
                name.FontWeight = FontWeights.SemiBold;
                name.Foreground = new SolidColorBrush(cFg);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                identity.Children.Add(name);
                TextBlock description = new TextBlock();
                description.Text = string.IsNullOrEmpty(def.Note) ? "套餐用量" : def.Note;
                description.FontSize = 9.2;
                description.Foreground = new SolidColorBrush(cMuted);
                description.TextTrimming = TextTrimming.CharacterEllipsis;
                description.Margin = new Thickness(0, 2, 8, 0);
                identity.Children.Add(description);
                Grid.SetColumn(identity, 1);
                head.Children.Add(identity);

                Border statusPill = new Border();
                statusPill.CornerRadius = new CornerRadius(7);
                statusPill.Background = new SolidColorBrush(cChip);
                statusPill.Padding = new Thickness(7, 2, 7, 2);
                statusPill.Margin = new Thickness(6, 0, 8, 0);
                TextBlock status = new TextBlock();
                status.FontSize = 9.6;
                status.Foreground = new SolidColorBrush(cMuted);
                status.VerticalAlignment = VerticalAlignment.Center;
                status.TextTrimming = TextTrimming.CharacterEllipsis;
                status.MaxWidth = 126;
                statusPill.Child = status;
                Grid.SetColumn(statusPill, 2);
                head.Children.Add(statusPill);
                CheckBox toggle = new CheckBox();
                toggle.Style = (Style)root.Resources["Switch"];
                toggle.Content = "";
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
                Grid.SetColumn(toggle, 3);
                toggle.VerticalAlignment = VerticalAlignment.Center;
                head.Children.Add(toggle);
                stack.Children.Add(head);
                planStatus[def.Id] = status;

                StackPanel windows = new StackPanel();
                windows.Margin = new Thickness(0, 8, 0, 0);
                planWindowHosts[def.Id] = windows;
                stack.Children.Add(windows);

                row.Child = stack;
                list.Children.Add(row);
                keyList.Children.Add(BuildPlanCredentialRow(def));
            }
        }

        private FrameworkElement BuildPlanCredentialRow(PlanDef def)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(10);
            row.Background = new SolidColorBrush(cField);
            row.BorderBrush = new SolidColorBrush(cBorder);
            row.BorderThickness = new Thickness(1);
            row.Padding = new Thickness(11, 8, 11, 9);
            row.Margin = new Thickness(0, 0, 0, 7);
            row.ToolTip = string.IsNullOrEmpty(def.Note) ? def.Name : def.Note;
            StackPanel stack = new StackPanel();

            DockPanel head = new DockPanel();
            TextBlock name = new TextBlock();
            name.Text = def.Name + " · 接入凭据";
            name.FontSize = 10.8;
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = new SolidColorBrush(cFg2);
            head.Children.Add(name);
            bool configured = cfg.GetStringAt("plans." + def.Id + ".apiKey", "") != "";
            TextBlock source = new TextBlock();
            source.Text = configured ? "已配置" : (def.Auth == "codexToken" ? "可读取本机登录" : "未配置");
            source.FontSize = 9.5;
            source.Foreground = new SolidColorBrush(configured ? cOk : cMuted);
            source.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(source, Dock.Right);
            head.Children.Add(source);
            stack.Children.Add(head);

            if (def.IsCustom)
            {
                stack.Children.Add(FieldRow("URL", "plans.custom.url", 0, 44));
                stack.Children.Add(FieldRow("路径", "plans.custom.path", 0, 44));
            }

            DockPanel keyRow = new DockPanel();
            keyRow.Margin = new Thickness(0, 7, 0, 0);
            TextBlock keyLabel = new TextBlock();
            keyLabel.Text = "API Key";
            keyLabel.FontSize = 9.5;
            keyLabel.Foreground = new SolidColorBrush(cMuted);
            keyLabel.Width = 54;
            keyLabel.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(keyLabel, Dock.Left);
            keyRow.Children.Add(keyLabel);
            Button apply = new Button();
            apply.Style = (Style)root.Resources["Mini"];
            apply.Content = "应用";
            apply.Width = 46;
            apply.Padding = new Thickness(0);
            DockPanel.SetDock(apply, Dock.Right);
            apply.Click += delegate { ApplyPlanKey(def.Id); };
            Button clear = new Button();
            clear.Style = (Style)root.Resources["Mini"];
            clear.Content = "清除";
            clear.Width = 46;
            clear.Padding = new Thickness(0);
            clear.Margin = new Thickness(0, 0, 6, 0);
            DockPanel.SetDock(clear, Dock.Right);
            clear.Click += delegate { ClearPlanKey(def.Id); };
            keyRow.Children.Add(apply);
            keyRow.Children.Add(clear);
            PasswordBox box = new PasswordBox();
            box.Style = (Style)root.Resources["KeyBox"];
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) ApplyPlanKey(def.Id);
            };
            planBoxes[def.Id] = box;
            keyRow.Children.Add(BuildSecretInput(box,
                cfg.GetStringAt("plans." + def.Id + ".apiKey", ""), "输入套餐 API Key"));
            stack.Children.Add(keyRow);

            if (def.Auth == "codexToken")
            {
                TextBlock hint = new TextBlock();
                hint.Text = "留空时自动读取本机 Codex 登录；也可手动填入 access token。";
                hint.FontSize = 9.5;
                hint.Foreground = new SolidColorBrush(cMuted);
                hint.TextWrapping = TextWrapping.Wrap;
                hint.Margin = new Thickness(0, 5, 0, 0);
                stack.Children.Add(hint);
            }

            row.Child = stack;
            return row;
        }

        private void ApplyPlanKey(string id)
        {
            PasswordBox box = planBoxes[id];
            string text = box.Password.Trim();
            if (text.Length == 0) return;
            cfg.SetAt("plans." + id + ".apiKey", Secret.Protect(text));
            box.Password = "";
            cfg.Save();
            BuildPlanRows();
            engine.Data.Refresh();
            Update();
        }

        private void ClearPlanKey(string id)
        {
            cfg.SetAt("plans." + id + ".apiKey", "");
            cfg.Save();
            BuildPlanRows();
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
                    text = "已连接";
                    colour = string.Format("#{0:X2}{1:X2}{2:X2}", cOk.R, cOk.G, cOk.B);
                }
                else if (state.Unsupported)
                {
                    text = state.Error;
                    colour = "#8FA0C8";
                }
                else
                {
                    text = state.Error;
                    colour = string.Format("#{0:X2}{1:X2}{2:X2}", cErr.R, cErr.G, cErr.B);
                }
                if (text != null && text.Length > 30) text = text.Substring(0, 30) + "…";
                pair.Value.Text = text;
                pair.Value.Foreground = Brush(colour);
                pair.Value.ToolTip = state == null ? null
                    : (state.Ok ? state.Summary() + (string.IsNullOrEmpty(state.KeySource)
                        ? "" : "\n" + state.KeySource) : state.ErrorDetail);

                StackPanel windows;
                if (!planWindowHosts.TryGetValue(id, out windows)) continue;
                windows.Children.Clear();
                if (!on)
                {
                    windows.Children.Add(PlanStatusLine("开启后显示套餐窗口"));
                }
                else if (state == null)
                {
                    windows.Children.Add(PlanStatusLine("等待用量数据"));
                }
                else if (state.Ok)
                {
                    if (state.Windows.Count == 0)
                    {
                        windows.Children.Add(PlanStatusLine("接口未返回用量窗口"));
                    }
                    else
                    {
                        foreach (PlanWindow window in state.Windows)
                            windows.Children.Add(BuildPlanWindowRow(window));
                    }
                }
                else
                {
                    windows.Children.Add(PlanStatusLine(
                        string.IsNullOrEmpty(state.Error) ? "查询失败" : state.Error));
                }
            }
        }

        private FrameworkElement BuildPlanWindowRow(PlanWindow window)
        {
            Grid row = new Grid();
            row.Margin = new Thickness(0, 2, 0, 7);
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid title = new Grid();
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock label = new TextBlock();
            label.Text = window.Label;
            label.FontSize = 10;
            label.Foreground = new SolidColorBrush(cMuted);
            title.Children.Add(label);
            Color percentColor = window.Percent >= 90 ? cErr
                : (window.Percent >= 70 ? Parse("#D88214") : cOk);
            TextBlock percent = new TextBlock();
            percent.Text = window.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            percent.FontSize = 10.5;
            percent.FontWeight = FontWeights.SemiBold;
            percent.Foreground = new SolidColorBrush(percentColor);
            percent.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(percent, 1);
            title.Children.Add(percent);
            row.Children.Add(title);

            Grid bar = new Grid();
            bar.Height = 5;
            bar.Margin = new Thickness(0, 4, 0, 0);
            double ratio = Math.Max(0.001, Math.Min(1, window.Percent / 100.0));
            bar.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(ratio, GridUnitType.Star)
            });
            bar.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(0.001, 1 - ratio), GridUnitType.Star)
            });
            Border fill = new Border();
            fill.CornerRadius = new CornerRadius(3);
            fill.Background = new SolidColorBrush(percentColor);
            bar.Children.Add(fill);
            Border rest = new Border();
            rest.CornerRadius = new CornerRadius(3);
            rest.Background = new SolidColorBrush(cChip);
            Grid.SetColumn(rest, 1);
            bar.Children.Add(rest);
            Grid.SetRow(bar, 1);
            row.Children.Add(bar);

            string reset = window.ResetDisplay();
            TextBlock resetText = new TextBlock();
            resetText.Text = string.IsNullOrEmpty(reset) ? "重置时间未提供" : reset;
            resetText.FontSize = 9;
            resetText.Foreground = new SolidColorBrush(cMuted);
            resetText.TextTrimming = TextTrimming.CharacterEllipsis;
            resetText.ToolTip = resetText.Text;
            resetText.Margin = new Thickness(0, 3, 0, 0);
            Grid.SetRow(resetText, 2);
            row.Children.Add(resetText);
            return row;
        }

        private TextBlock PlanStatusLine(string text)
        {
            TextBlock line = new TextBlock();
            line.Text = text;
            line.FontSize = 9.5;
            line.Foreground = new SolidColorBrush(cMuted);
            line.TextWrapping = TextWrapping.Wrap;
            line.ToolTip = text;
            return line;
        }

        private static SolidColorBrush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        // ------------------------------------------------------------ 显示 / 刷新
        /// <summary>
        /// 面板阴影与气泡同款：阴影单独一层，整块区域减去卡片的圆角矩形，
        /// 只让阴影出现在卡片轮廓之外；不然圆角一大，四角缺口里就会透出阴影色。
        /// </summary>
        private void UpdatePanelShadowClip()
        {
            if (panelShadowLayer == null || panelShadowShape == null || panelCard == null) return;
            double w = panelCard.ActualWidth;
            double h = panelCard.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                panelShadowLayer.Clip = null;
                return;
            }
            double radius = panelCard.CornerRadius.TopLeft;
            panelShadowShape.CornerRadius = new CornerRadius(radius);
            RectangleGeometry outer = new RectangleGeometry(
                new Rect(0, 0, w + PanelShadowPad * 2, h + PanelShadowPad * 2));
            RectangleGeometry inner = new RectangleGeometry(
                new Rect(PanelShadowPad, PanelShadowPad, w, h), radius, radius);
            panelShadowLayer.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        }

        private async void RefreshPanelQuote()
        {
            int request = ++panelQuoteRequest;
            if (!cfg.GetFlag("alert", "quipsOn", true))
            {
                panelQuote = "余额、套餐与宠物设置，都在这里。";
                ApplyPanelQuote();
                return;
            }

            panelQuote = PickPanelQuote();
            ApplyPanelQuote();
            string online = await FetchHitokotoQuoteAsync();
            if (request != panelQuoteRequest || !IsVisible || string.IsNullOrEmpty(online)) return;
            panelQuote = online;
            lastPanelQuote = panelQuote;
            ApplyPanelQuote();
        }

        private string PickPanelQuote()
        {
            string picked = "";
            for (int i = 0; i < 4; i++)
            {
                string candidate = engine.QuipFor("greeting");
                if (!string.IsNullOrEmpty(candidate)
                    && !string.Equals(candidate, lastPanelQuote, StringComparison.Ordinal))
                {
                    picked = candidate;
                    break;
                }
            }
            if (string.IsNullOrEmpty(picked))
            {
                for (int i = 0; i < 6; i++)
                {
                    string candidate = PanelQuoteFallbacks[Program.Rng.Next(PanelQuoteFallbacks.Length)];
                    if (!string.Equals(candidate, lastPanelQuote, StringComparison.Ordinal))
                    {
                        picked = candidate;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(picked)) picked = PanelQuoteFallbacks[0];
            lastPanelQuote = picked;
            return picked;
        }

        private async Task<string> FetchHitokotoQuoteAsync()
        {
            try
            {
                string text = await Http.For(cfg, 10)
                    .GetStringAsync("https://v1.hitokoto.cn/?c=d&c=i&c=k&encode=text&charset=utf-8");
                return NormalizePanelQuote(text);
            }
            catch
            {
                return "";
            }
        }

        private static string NormalizePanelQuote(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string value = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (value.Length > 72) value = value.Substring(0, 72).TrimEnd() + "…";
            return value;
        }

        private async void RefreshPanelWeather()
        {
            int request = ++panelWeatherRequest;
            string city = cfg.GetStringAt("weather.city", "上海").Trim();
            if (!cfg.GetFlag("weather", "enabled", true) || city.Length == 0)
            {
                panelWeather = "";
                ApplyPanelQuote();
                return;
            }

            panelWeather = LoadPanelWeatherCache(city);
            ApplyPanelQuote();
            string weather = await FetchPanelWeatherAsync(city);
            if (request != panelWeatherRequest || !IsVisible || string.IsNullOrEmpty(weather)) return;
            panelWeather = weather;
            SavePanelWeatherCache(city, weather);
            ApplyPanelQuote();
        }

        private void SaveWeatherCity()
        {
            TextBox box = ui["WeatherCityBox"] as TextBox;
            if (box == null) return;
            string city = box.Text.Trim();
            string previous = cfg.GetStringAt("weather.city", "上海");
            cfg.SetAt("weather.city", city);
            if (!string.Equals(previous, city, StringComparison.Ordinal))
            {
                cfg.SetAt("weather.latitude", 0.0);
                cfg.SetAt("weather.longitude", 0.0);
                ClearPanelWeatherCache();
            }
            cfg.Save();
            UpdateSaveState(HasDraftChanges());
            RefreshPanelWeather();
        }

        private async void LocateWeatherAsync()
        {
            Button button = ui["WeatherLocateBtn"] as Button;
            if (button == null) return;
            button.IsEnabled = false;
            button.Content = "定位中";
            try
            {
                DetectedWeatherLocation location = await FetchDetectedWeatherLocationAsync();
                if (location == null || string.IsNullOrEmpty(location.Label))
                {
                    button.Content = "失败";
                    await Task.Delay(1200);
                    return;
                }
                cfg.SetAt("weather.enabled", true);
                cfg.SetAt("weather.city", location.Label);
                cfg.SetAt("weather.latitude", location.Latitude);
                cfg.SetAt("weather.longitude", location.Longitude);
                cfg.Save();
                TextBox box = ui["WeatherCityBox"] as TextBox;
                if (box != null) box.Text = location.Label;
                CheckBox enabled = ui["WeatherChk"] as CheckBox;
                if (enabled != null) enabled.IsChecked = true;
                ClearPanelWeatherCache();
                panelWeather = "";
                ApplyPanelQuote();
                UpdateSaveState(HasDraftChanges());
                RefreshPanelWeather();
                button.Content = "已定位";
                await Task.Delay(900);
            }
            catch
            {
                button.Content = "失败";
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "定位";
            }
        }

        private async Task<DetectedWeatherLocation> FetchDetectedWeatherLocationAsync()
        {
            DetectedWeatherLocation location = await FetchIpWeatherLocationAsync();
            if (location == null) return null;
            string label = await ReverseGeocodeWeatherLocationAsync(location);
            if (!string.IsNullOrEmpty(label)) location.Label = label;
            return location;
        }

        private async Task<DetectedWeatherLocation> FetchIpWeatherLocationAsync()
        {
            try
            {
                string body = await Http.For(cfg, 12).GetStringAsync("https://ipwho.is/");
                Dictionary<string, object> root = Json.ReadText(body);
                if (Json.ToBool(Json.Get(root, "success"), true))
                {
                    DetectedWeatherLocation location = LocationFromJson(root, "latitude", "longitude", "city", "region");
                    if (location != null) return location;
                }
            }
            catch
            {
            }
            try
            {
                string body = await Http.For(cfg, 12).GetStringAsync("https://ipapi.co/json/");
                Dictionary<string, object> root = Json.ReadText(body);
                if (!Json.ToBool(Json.Get(root, "error"), false))
                    return LocationFromJson(root, "latitude", "longitude", "city", "region");
            }
            catch
            {
            }
            return null;
        }

        private static DetectedWeatherLocation LocationFromJson(
            Dictionary<string, object> root, string latitudeKey, string longitudeKey,
            string cityKey, string regionKey)
        {
            double latitude = Json.ToNumber(Json.Get(root, latitudeKey), double.NaN);
            double longitude = Json.ToNumber(Json.Get(root, longitudeKey), double.NaN);
            string city = Convert.ToString(Json.Get(root, cityKey), CultureInfo.InvariantCulture);
            string region = Convert.ToString(Json.Get(root, regionKey), CultureInfo.InvariantCulture);
            if (double.IsNaN(latitude) || double.IsNaN(longitude)
                || latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180) return null;
            string label = string.IsNullOrEmpty(city) ? region : city;
            if (string.IsNullOrEmpty(label)) return null;
            DetectedWeatherLocation location = new DetectedWeatherLocation();
            location.Label = label;
            location.Latitude = latitude;
            location.Longitude = longitude;
            return location;
        }

        private async Task<string> ReverseGeocodeWeatherLocationAsync(DetectedWeatherLocation location)
        {
            try
            {
                string url = "https://nominatim.openstreetmap.org/reverse?lat="
                    + location.Latitude.ToString("0.######", CultureInfo.InvariantCulture)
                    + "&lon=" + location.Longitude.ToString("0.######", CultureInfo.InvariantCulture)
                    + "&format=jsonv2&addressdetails=1&zoom=14&accept-language=zh-CN";
                string body = await GetNominatimJsonAsync(url);
                Dictionary<string, object> root = Json.ReadText(body);
                Dictionary<string, object> address = Json.AsObject(Json.Get(root, "address"));
                string city = FirstAddressValue(address, "city", "town", "municipality", "county", "state");
                string district = FirstAddressValue(address, "city_district", "district", "suburb", "borough", "quarter");
                if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(district)
                    && !string.Equals(city, district, StringComparison.Ordinal))
                    return city + " " + district;
                if (!string.IsNullOrEmpty(district)) return district;
                if (!string.IsNullOrEmpty(city)) return city;
            }
            catch
            {
            }
            return location.Label;
        }

        private static string FirstAddressValue(Dictionary<string, object> address, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value = Convert.ToString(Json.Get(address, key), CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(value))
                {
                    value = value.Trim();
                    int duplicate = value.IndexOf(';');
                    if (duplicate > 0) value = value.Substring(0, duplicate).Trim();
                    if (!string.IsNullOrEmpty(value)) return value;
                }
            }
            return "";
        }

        private async Task<string> GetNominatimJsonAsync(string url)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd("BalancePet/1.2 weather-location");
                request.Headers.AcceptLanguage.ParseAdd("zh-CN");
                using (HttpResponseMessage response = await Http.For(cfg, 15).SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        private static void ClearPanelWeatherCache()
        {
            try
            {
                string path = System.IO.Path.Combine(AppPaths.DataRoot, "weather-cache.json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static string LoadPanelWeatherCache(string city)
        {
            try
            {
                string path = System.IO.Path.Combine(AppPaths.DataRoot, "weather-cache.json");
                Dictionary<string, object> cached = Json.ReadObject(path);
                string cachedCity = Convert.ToString(Json.Get(cached, "city"), CultureInfo.InvariantCulture);
                string text = Convert.ToString(Json.Get(cached, "text"), CultureInfo.InvariantCulture);
                long atMs = (long)Json.ToNumber(Json.Get(cached, "atMs"), 0);
                long nowMs = (long)(DateTime.UtcNow
                    - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                if (!string.Equals(cachedCity, city, StringComparison.Ordinal)
                    || string.IsNullOrEmpty(text) || nowMs - atMs > 6L * 60 * 60 * 1000) return "";
                return text;
            }
            catch
            {
                return "";
            }
        }

        private static void SavePanelWeatherCache(string city, string text)
        {
            try
            {
                Dictionary<string, object> cached = new Dictionary<string, object>();
                cached["city"] = city;
                cached["text"] = text;
                cached["atMs"] = (long)(DateTime.UtcNow
                    - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                Json.WriteObject(System.IO.Path.Combine(AppPaths.DataRoot, "weather-cache.json"), cached);
            }
            catch
            {
            }
        }

        private async Task<string> FetchPanelWeatherAsync(string city)
        {
            try
            {
                double latitude = cfg.GetNumberAt("weather.latitude", 0);
                double longitude = cfg.GetNumberAt("weather.longitude", 0);
                bool hasCoordinates = !double.IsNaN(latitude) && !double.IsNaN(longitude)
                    && latitude >= -90 && latitude <= 90
                    && longitude >= -180 && longitude <= 180
                    && (Math.Abs(latitude) > 0.0001 || Math.Abs(longitude) > 0.0001);
                string name = city;
                if (!hasCoordinates && !KnownWeatherCoordinates(city, out latitude, out longitude))
                {
                    string geoUrl = "https://nominatim.openstreetmap.org/search?q="
                        + Uri.EscapeDataString(city)
                        + "&format=jsonv2&addressdetails=1&limit=1&accept-language=zh-CN";
                    string geoBody = await GetNominatimJsonAsync(geoUrl);
                    List<object> results = Json.AsArray(Json.ReadAny(geoBody));
                    if (results.Count == 0) return "";
                    Dictionary<string, object> location = results[0] as Dictionary<string, object>;
                    if (location == null) return "";
                    latitude = Json.ToNumber(Json.Get(location, "lat"), double.NaN);
                    longitude = Json.ToNumber(Json.Get(location, "lon"), double.NaN);
                    if (double.IsNaN(latitude) || double.IsNaN(longitude) || string.IsNullOrEmpty(name)) return "";
                }

                string weatherUrl = string.Format(CultureInfo.InvariantCulture,
                    "https://api.open-meteo.com/v1/forecast?latitude={0:0.####}&longitude={1:0.####}"
                    + "&current=temperature_2m,weather_code&timezone=auto",
                    latitude, longitude);
                string weatherBody = await Http.For(cfg, 15).GetStringAsync(weatherUrl);
                Dictionary<string, object> forecast = Json.ReadText(weatherBody);
                Dictionary<string, object> current = Json.AsObject(Json.Get(forecast, "current"));
                double temperature = Json.ToNumber(Json.Get(current, "temperature_2m"), double.NaN);
                int weatherCode = (int)Json.ToNumber(Json.Get(current, "weather_code"), -1);
                if (double.IsNaN(temperature)) return "";
                return name + " " + temperature.ToString("0", CultureInfo.InvariantCulture)
                    + "°C " + WeatherCodeLabel(weatherCode);
            }
            catch
            {
                return "";
            }
        }

        private static bool KnownWeatherCoordinates(string city, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;
            switch (city)
            {
                case "上海": case "上海市": latitude = 31.2222; longitude = 121.4581; return true;
                case "北京": case "北京市": latitude = 39.9042; longitude = 116.4074; return true;
                case "广州": case "广州市": latitude = 23.1291; longitude = 113.2644; return true;
                case "深圳": case "深圳市": latitude = 22.5431; longitude = 114.0579; return true;
                case "杭州": case "杭州市": latitude = 30.2741; longitude = 120.1551; return true;
                case "成都": case "成都市": latitude = 30.5728; longitude = 104.0668; return true;
                case "重庆": case "重庆市": latitude = 29.5630; longitude = 106.5516; return true;
                case "武汉": case "武汉市": latitude = 30.5928; longitude = 114.3055; return true;
                case "南京": case "南京市": latitude = 32.0603; longitude = 118.7969; return true;
                case "西安": case "西安市": latitude = 34.3416; longitude = 108.9398; return true;
                case "苏州": case "苏州市": latitude = 31.2989; longitude = 120.5853; return true;
                case "天津": case "天津市": latitude = 39.3434; longitude = 117.3616; return true;
                case "长沙": case "长沙市": latitude = 28.2282; longitude = 112.9388; return true;
                case "郑州": case "郑州市": latitude = 34.7466; longitude = 113.6254; return true;
                case "青岛": case "青岛市": latitude = 36.0671; longitude = 120.3826; return true;
                case "厦门": case "厦门市": latitude = 24.4798; longitude = 118.0894; return true;
                default: return false;
            }
        }

        private static string WeatherCodeLabel(int code)
        {
            if (code == 0) return "晴";
            if (code == 1) return "少云";
            if (code == 2) return "多云";
            if (code == 3) return "阴";
            if (code == 45 || code == 48) return "有雾";
            if (code >= 51 && code <= 57) return "毛毛雨";
            if (code >= 61 && code <= 67) return "有雨";
            if (code >= 71 && code <= 77) return "有雪";
            if (code >= 80 && code <= 82) return "阵雨";
            if (code >= 85 && code <= 86) return "阵雪";
            if (code >= 95 && code <= 99) return "雷雨";
            return "天气未知";
        }

        private void ApplyPanelQuote()
        {
            if (!ui.ContainsKey("SubText")) return;
            TextBlock label = ui["SubText"] as TextBlock;
            if (label == null || string.IsNullOrEmpty(panelQuote)) return;
            string text = panelQuote;
            if (!string.IsNullOrEmpty(panelWeather)) text += "  ·  " + panelWeather;
            label.Text = text;
            label.ToolTip = text;
        }

        public void ShowFor(PetWindow pet)
        {
            ActivePet = pet;
            RefreshPanelQuote();
            RefreshPanelWeather();
            RefreshActionControls();
            Update();
            BuildRoleButtons();   // roles\ 可能被外部增删：每次打开都重新扫描
            root.Visibility = Visibility.Visible;
            if (pet != null)
            {
                // Rough placement first, then refine once the window has measured itself.
                // 窗口四周留了 PanelShadowPad 的阴影余量，摆放时扣掉，卡片才会贴着宠物。
                root.Left = Math.Round(pet.X + pet.Width + 12 - PanelShadowPad, 1);
                root.Top = Math.Round(pet.Y - PanelShadowPad, 1);
            }
            root.Show();
            root.UpdateLayout();
            UpdatePanelShadowClip();
            if (pet != null)
            {
                ScreenArea area = ScreenArea.ForPoint(pet.X + pet.Width / 2, pet.Y + pet.Height / 2);
                double cardW = Math.Max(0, root.ActualWidth - PanelShadowPad * 2);
                double cardH = Math.Max(0, root.ActualHeight - PanelShadowPad * 2);
                double x = pet.X + pet.Width + 12;
                if (x + cardW > area.Right - 4) x = pet.X - cardW - 12;
                x = Math.Max(area.Left + 4, Math.Min(x, area.Right - cardW - 4));
                double y = Math.Max(area.Top + 4, Math.Min(pet.Y, area.Bottom - cardH - 4));
                root.Left = Math.Round(x - PanelShadowPad, 1);
                root.Top = Math.Round(y - PanelShadowPad, 1);
            }
            root.Activate();
        }

        private void UpdateSaveState(bool pending)
        {
            if (root == null || !ui.ContainsKey("SaveStateText")) return;
            TextBlock state = ui["SaveStateText"] as TextBlock;
            Button save = ui["SaveSettingsBtn"] as Button;
            if (state != null)
            {
                state.Text = pending ? "未保存 · 已记忆" : "已保存";
                state.Foreground = new SolidColorBrush(pending ? cErr : cOk);
                state.ToolTip = pending
                    ? "修改已写入设置草稿，关闭后仍会保留"
                    : "当前设置与正式配置一致";
            }
            if (save != null) save.IsEnabled = pending;
        }

        private bool HasDraftChanges()
        {
            return !string.Equals(cfg.SerializeSnapshot(), savedConfigSnapshot,
                StringComparison.Ordinal);
        }

        private void SaveDraftConfig()
        {
            savingConfig = true;
            try
            {
                engine.CommitDraftConfig(cfg);
                savedConfigSnapshot = cfg.SerializeSnapshot();
                try
                {
                    if (File.Exists(draftConfigPath)) File.Delete(draftConfigPath);
                }
                catch
                {
                }
                UpdateSaveState(false);
                RebuildForAccent(true);
            }
            finally
            {
                savingConfig = false;
            }
        }

        public void HidePanel()
        {
            if (root != null)
            {
                if (root.Visibility == Visibility.Visible) LastHiddenAt = DateTime.UtcNow;
                root.Visibility = Visibility.Hidden;
            }
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
                double shownScale = ActivePet != null ? ActivePet.Scale : cfg.GetNumber("", "scale", 1.0);
                ((Slider)ui["ScaleSlider"]).Value = Math.Max(0.4, Math.Min(2.6, shownScale));
                ((TextBlock)ui["ScaleVal"]).Text = shownScale.ToString("0.00", CultureInfo.InvariantCulture) + "x";
                ((Slider)ui["OpacitySlider"]).Value = cfg.GetNumber("", "opacity", 1.0);
                ((TextBlock)ui["OpacityVal"]).Text = ((int)Math.Round(cfg.GetNumber("", "opacity", 1.0) * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                ((Slider)ui["SpeedSlider"]).Value = cfg.GetNumber("walk", "speedPxPerSec", 72);
                ((TextBlock)ui["SpeedVal"]).Text = ((int)cfg.GetNumber("walk", "speedPxPerSec", 72)).ToString(CultureInfo.InvariantCulture);
                ((Slider)ui["ActionPauseSlider"]).Value = Math.Max(6, Math.Min(300, cfg.GetNumber("actions", "pauseMinSec", 14)));
                UpdateActionPauseLabel();
                SyncBubbleSlider("ActionDurationSlider", "ActionDurationVal", cfg.GetNumberAt("actions.durationScale", 1), "0.0x");
                ((CheckBox)ui["AutoActionsChk"]).IsChecked = cfg.GetFlag("actions", "enabled", true);
                ((CheckBox)ui["WalkChk"]).IsChecked = cfg.GetFlag("walk", "enabled", true);
                ((CheckBox)ui["SnapChk"]).IsChecked = cfg.GetFlag("snap", "enabled", true);
                ((CheckBox)ui["SnapAutoChk"]).IsChecked = cfg.GetFlag("snap", "auto", false);
                SyncBubbleSlider("SnapDistanceSlider", "SnapDistanceVal", cfg.GetNumberAt("snap.distancePx", 56), "0 px");
                ((CheckBox)ui["SleepChk"]).IsChecked = cfg.GetFlag("sleep", "enabled", true);
                ((CheckBox)ui["QuipsChk"]).IsChecked = cfg.GetFlag("alert", "quipsOn", true);
                ((CheckBox)ui["CollideChk"]).IsChecked = cfg.GetFlagAt("pets.collide", true);
                ((CheckBox)ui["AlertChk"]).IsChecked = cfg.GetFlag("alert", "balanceWarn", true);
                ((CheckBox)ui["BudgetChk"]).IsChecked = cfg.GetFlag("alert", "budgetWarn", true);
                ((CheckBox)ui["QuotaChk"]).IsChecked = cfg.GetFlag("alert", "quotaWarn", true);
                ((CheckBox)ui["WeatherChk"]).IsChecked = cfg.GetFlag("weather", "enabled", true);
                ((CheckBox)ui["EdgeHideChk"]).IsChecked = cfg.GetFlag("edgeHide", "enabled", true);
                ((CheckBox)ui["CloneAutoChk"]).IsChecked = cfg.GetFlagAt("clones.auto", true);
                ((CheckBox)ui["RandomRoleChk"]).IsChecked = cfg.GetFlagAt("roles.random", true);
                RefreshRigScope();
                string fontNow = cfg.GetString("bubble", "font", "");
                string uiThemeNow = cfg.GetString("ui", "theme", "system");
                string bubbleThemeNow = cfg.GetString("bubble", "theme", "light");
                ((Button)ui["UiThemeDropBtn"]).Content = BuildThemeSelectorVisual(
                    DropLabel(UiThemeValues, UiThemeLabels, "ui.theme", "跟随系统"), uiThemeNow, false);
                ((Button)ui["ThemeDropBtn"]).Content = BuildThemeSelectorVisual(
                    DropLabel(ThemeValues, ThemeLabels, "bubble.theme", "浅色"), bubbleThemeNow, true);
                bool glassThemeActive =
                    string.Equals(uiThemeNow, "glass", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(bubbleThemeNow, "glass", StringComparison.OrdinalIgnoreCase);
                ui["BubbleGlassPanel"].Visibility = glassThemeActive
                    ? Visibility.Visible : Visibility.Collapsed;
                bool monetThemeActive =
                    string.Equals(uiThemeNow, "monet", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(bubbleThemeNow, "monet", StringComparison.OrdinalIgnoreCase);
                ui["BubbleMonetPanel"].Visibility = monetThemeActive
                    ? Visibility.Visible : Visibility.Collapsed;
                string monetCustom = cfg.GetString("bubble", "monetCustomColor", "");
                ThemePalette monetPalette = BubbleTheme.Resolve("monet", cfg);
                ((Border)ui["MonetSwatch"]).Background =
                    new SolidColorBrush(monetPalette.Accent);
                ((Border)ui["MonetSwatch"]).BorderBrush =
                    new SolidColorBrush(cBorder);
                ((TextBlock)ui["MonetColorVal"]).Text = string.IsNullOrEmpty(monetCustom)
                    ? "跟随壁纸" : monetCustom.ToUpperInvariant();
                ((Button)ui["EffectDropBtn"]).Content = DropLabel(EffectValues, EffectLabels, "bubble.effect", "关闭") + "  ▾";
                ((Button)ui["FontDropBtn"]).Content = DropLabel(FontValues, FontLabels, "bubble.font", "跟随系统") + "  ▾";
                ((Button)ui["NumberFontDropBtn"]).Content = DropLabel(NumberFontValues, NumberFontLabels, "bubble.numberFont", "跟随字体") + "  ▾";
                RefreshBubblePresetButton();
                string bubbleLayoutNow = cfg.GetString("bubble", "layout", "standard");
                ((Button)ui["BubbleLayoutBtn"]).Content = BuildBubbleLayoutVisual(bubbleLayoutNow, false);
                ((Button)ui["BubbleLayoutBtn"]).ToolTip = "气泡布局："
                    + DropLabel(BubbleLayoutValues, BubbleLayoutLabels, "bubble.layout", "原版")
                    + " · " + BubbleLayoutDescription(bubbleLayoutNow);
                bool customBubbleSize = string.Equals(bubbleLayoutNow,
                    "custom", StringComparison.OrdinalIgnoreCase);
                ui["BubbleCustomSizePanel"].Visibility = customBubbleSize
                    ? Visibility.Visible : Visibility.Collapsed;
                ((Button)ui["TrendRangeBtn"]).Content = DropLabel(TrendValues, TrendLabels, "bubble.trendDays", "近 7 天") + "  ▾";
                // 设置卡片本身也跟随同一个字体，保证整卡观感一致。
                root.FontFamily = string.IsNullOrEmpty(fontNow)
                    ? System.Windows.SystemFonts.MessageFontFamily
                    : new FontFamily(fontNow);
                ((CheckBox)ui["BubbleTrendChk"]).IsChecked = cfg.GetFlag("bubble", "trend", true);
                ((CheckBox)ui["BubbleTailChk"]).IsChecked = cfg.GetFlag("bubble", "tail", true);
                ((CheckBox)ui["CustomSplitChk"]).IsChecked = cfg.GetFlag("bubble", "customSplit", false);
                ((CheckBox)ui["CustomHeaderChk"]).IsChecked = cfg.GetFlag("bubble", "customHeader", true);
                ((CheckBox)ui["CustomIconChk"]).IsChecked = cfg.GetFlag("bubble", "customIcon", true);
                ((CheckBox)ui["CustomHeroChk"]).IsChecked = cfg.GetFlag("bubble", "customHero", true);
                ((CheckBox)ui["CustomDetailChk"]).IsChecked = cfg.GetFlag("bubble", "customDetail", true);
                ((CheckBox)ui["CustomMetricsChk"]).IsChecked = cfg.GetFlag("bubble", "customMetrics", true);
                ((CheckBox)ui["CustomChartChk"]).IsChecked = cfg.GetFlag("bubble", "customChart", true);
                ((CheckBox)ui["CustomQuipChk"]).IsChecked = cfg.GetFlag("bubble", "customQuip", true);
                ((CheckBox)ui["CustomFooterChk"]).IsChecked = cfg.GetFlag("bubble", "customFooter", true);
                double bubbleSpeed = cfg.GetNumber("bubble", "speed", 1.0);
                ((Slider)ui["BubbleSpeedSlider"]).Value = bubbleSpeed;
                ((TextBlock)ui["BubbleSpeedVal"]).Text = bubbleSpeed.ToString("0.0", CultureInfo.InvariantCulture) + "x";
                ((CheckBox)ui["BubbleReverseChk"]).IsChecked = cfg.GetFlag("bubble", "effectReverse", false);
                SyncBubbleSlider("BubbleDepthSlider", "BubbleDepthVal", cfg.GetNumberAt("bubble.effectDepth", 1), "0.0");
                SyncBubbleSlider("BubbleFontSlider", "BubbleFontVal", cfg.GetNumberAt("bubble.fontScale", 1), "0.0x");
                SyncBubbleSlider("BubbleWidthSlider", "BubbleWidthVal", cfg.GetNumberAt("bubble.customWidth", 228), "0");
                SyncBubbleSlider("BubbleHeightSlider", "BubbleHeightVal", cfg.GetNumberAt("bubble.customHeight", 220), "0");
                SyncBubbleSlider("BubbleRadiusSlider", "BubbleRadiusVal", cfg.GetNumberAt("bubble.radius", 16), "0");
                SyncBubbleSlider("BubbleTailSlider", "BubbleTailVal", cfg.GetNumberAt("bubble.tailSize", 13), "0");
                SyncBubbleSlider("BubbleOpacitySlider", "BubbleOpacityVal", cfg.GetNumberAt("bubble.opacity", 1), "0%");
                SyncBubbleSlider("BubbleShadowSlider", "BubbleShadowVal", cfg.GetNumberAt("bubble.shadow", 0.28), "0.00");
                SyncBubbleSlider("GlassOpacitySlider", "GlassOpacityVal",
                    cfg.GetNumberAt("bubble.glassOpacity", 0.745), "0%");
                SyncBubbleSlider("GlassHighlightSlider", "GlassHighlightVal",
                    cfg.GetNumberAt("bubble.glassHighlight", 0.47), "0%");
                SyncBubbleSlider("GlassBorderSlider", "GlassBorderVal",
                    cfg.GetNumberAt("bubble.glassBorder", 0.59), "0%");
                double fpsCap = Math.Max(30, deviceHz);
                // 显示器降级/换了屏（或旧配置写的是 120）时，把超出上限的值收回来写回配置。
                if (cfg.GetNumberAt("frames.activePerSecond", 25) > fpsCap)
                {
                    cfg.SetAt("frames.activePerSecond", fpsCap);
                    cfg.Save();
                }
                if (cfg.GetNumberAt("frames.idlePerSecond", 4) > fpsCap)
                {
                    cfg.SetAt("frames.idlePerSecond", fpsCap);
                    cfg.Save();
                }
                SyncBubbleSlider("FpsSlider", "FpsVal",
                    Math.Min(cfg.GetNumberAt("frames.activePerSecond", 25), fpsCap), "0 fps");
                SyncBubbleSlider("IdleFpsSlider", "IdleFpsVal",
                    Math.Min(cfg.GetNumberAt("frames.idlePerSecond", 4), fpsCap), "0 fps");
                SyncBubbleSlider("SliverSlider", "SliverVal", cfg.GetNumberAt("edgeHide.sliverRatio", 0.32), "0%");
                MarkPalette("Color1Panel", cfg.GetStringAt("bubble.color1", "#4B6BFB"));
                MarkMixPalette();
                UpdatePreview();
            RefreshActionPreviewVisual();
                BuildAlertRows();
                ((TextBox)ui["EdgeDelayBox"]).Text = cfg.GetNumberAt("edgeHide.delaySec", 10).ToString("0.#", CultureInfo.InvariantCulture);
                ((TextBox)ui["CloneMaxBox"]).Text = engine.ConfiguredCloneCap().ToString(CultureInfo.InvariantCulture);
                int clonePause = (int)cfg.GetNumberAt("clones.pauseMinSec", 60);
                ((Slider)ui["ClonePauseSlider"]).Value = Math.Max(10, Math.Min(600, clonePause));
                double cloneJitterPct = cfg.GetNumberAt("clones.jitter", 0.5) * 100;
                ((Slider)ui["CloneJitterSlider"]).Value = Math.Max(0, Math.Min(150, cloneJitterPct));
                ((TextBlock)ui["CloneJitterVal"]).Text = "+" + ((int)Math.Round(cloneJitterPct)).ToString(CultureInfo.InvariantCulture) + "%";
                UpdateClonePauseLabel();
                ((Button)ui["CloneRoleBtn"]).Content = "分裂外观 · " + CloneRoleLabel() + "  ▾";
                string cloneMode = cfg.GetString("clones", "roleMode", "random");
                TextBlock cloneHint = (TextBlock)ui["CloneRoleHint"];
                if (string.Equals(cloneMode, "random", StringComparison.OrdinalIgnoreCase))
                    cloneHint.Text = "新分身会随机选择角色。";
                else if (string.Equals(cloneMode, "current", StringComparison.OrdinalIgnoreCase))
                    cloneHint.Text = "新分身沿用当前主宠的角色。";
                else if (string.Equals(cloneMode, "slots", StringComparison.OrdinalIgnoreCase))
                    cloneHint.Text = "主宠在外观页选择；下面分别为第 2 只及之后的分身指定角色。";
                else
                    cloneHint.Text = "新分身固定使用所选角色；循环涨落恢复时仍保留它原来的角色。";
                BuildCloneSlotRows();
                ((CheckBox)ui["CloneCycleChk"]).IsChecked = cfg.GetFlagAt("clones.cycle", true);
                ((TextBox)ui["CloneMinBox"]).Text = ((int)engine.CloneFloor()).ToString(CultureInfo.InvariantCulture);
                ((TextBox)ui["ProxyBox"]).Text = cfg.GetStringAt("network.proxy", "");
                ((TextBox)ui["WeatherCityBox"]).Text = cfg.GetStringAt("weather.city", "上海");
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
                RefreshControlAvailability();
            }
            finally
            {
                updating = false;
            }
            UpdateSaveState(HasDraftChanges());
            RefreshPreviewDock();
        }

        public void UpdateDataText()
        {
            if (root == null) return;
            // 面板标题可自定义（ui.title）；默认用通用名，不绑定具体平台。
            string title = cfg.GetStringAt("ui.title", "余额桌宠");
            if (string.IsNullOrEmpty(title)) title = "余额桌宠";
            ((TextBlock)ui["TitleText"]).Text = title;
            root.Title = title;
            ApplyPanelQuote();
            UpdateProviderStatus();
            UpdatePlanStatus();
            RefreshPreviewDock();
        }

        // ------------------------------------------------------------ 气泡强调色 / 动态配色
        // 预置色不再铺在主界面：统一收进调色盘底部，主界面只展示"当前选中"。
        private static readonly string[] ColorPresets = new string[]
        {
            "#4B6BFB", "#7C5CFF", "#A855F7", "#E8517B", "#F08A24", "#FBBF24", "#12B5A5", "#10B981", "#38BDF8", "#F472B6",
            "#F87171", "#0EA5E9"
        };

        // 主题 / 特效 / 字体：下拉选项（简单、好懂，一处设置）
        private static readonly string[] ThemeValues = new string[] { "light", "dim", "dark", "system", "glass", "frost", "monet" };
        private static readonly string[] ThemeLabels = new string[] { "浅色", "浅黑", "深黑", "跟随系统", "液态玻璃", "磨砂", "莫奈取色" };
        private static readonly string[] UiThemeValues = ThemeValues;
        private static readonly string[] UiThemeLabels = ThemeLabels;
        private static readonly string[] BubbleLayoutValues = BubbleLayouts.LayoutValues();
        private static readonly string[] BubbleLayoutLabels = BubbleLayouts.LayoutLabels();
        // 动效（独立维度，和"混合配色"任意组合）
        private static readonly string[] EffectValues = new string[] { "off", "flow", "swirl", "breathe", "shimmer", "shift", "rainbow" };
        private static readonly string[] EffectLabels = new string[] { "关闭", "流动", "旋转", "呼吸", "闪动", "漂移", "彩虹" };
        private static readonly string[] FontValues = new string[]
        {
            "", "Microsoft YaHei UI", "DengXian", "SimSun", "KaiTi", "Microsoft YaHei"
        };
        private static readonly string[] FontLabels = new string[]
        {
            "跟随系统", "微软雅黑 UI", "等线", "宋体", "楷体", "微软雅黑"
        };
        private static readonly string[] NumberFontValues = new string[]
        {
            "", "Cascadia Mono, Consolas, Courier New"
        };
        private static readonly string[] NumberFontLabels = new string[] { "跟随字体", "等宽数字（数字不跳动）" };
        private static readonly string[] TrendValues = new string[] { "7", "14", "30" };
        private static readonly string[] TrendLabels = new string[] { "近 7 天", "近 14 天", "近 30 天" };

        private string CloneSlotRole(int slot)
        {
            Dictionary<string, object> selected = cfg.GetObject("", "cloneSlots");
            object value;
            if (selected.TryGetValue(slot.ToString(CultureInfo.InvariantCulture), out value))
            {
                return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            return "@current";
        }

        private string CloneSlotRoleLabel(int slot)
        {
            string role = CloneSlotRole(slot);
            if (string.Equals(role, "@current", StringComparison.OrdinalIgnoreCase)) return "跟随主宠";
            if (string.IsNullOrEmpty(role)) return "内置小鲸鱼";
            PetPack pack = PetPack.TryLoad(cfg, role);
            return pack == null || string.IsNullOrEmpty(pack.Name)
                ? System.IO.Path.GetFileNameWithoutExtension(role)
                : PublicPackName(pack.Name);
        }

        private static ScrollViewer ScrollableOptions(UIElement content, double maxHeight)
        {
            ScrollViewer scroll = new ScrollViewer();
            scroll.MaxHeight = maxHeight;
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.CanContentScroll = true;
            scroll.PanningMode = PanningMode.VerticalOnly;
            scroll.Content = content;
            return scroll;
        }

        private static void BringSelectedOptionIntoView(Popup popup, Button item)
        {
            if (popup == null || item == null) return;
            popup.Dispatcher.BeginInvoke(new Action(delegate { item.BringIntoView(); }),
                DispatcherPriority.Loaded);
        }

        private void BuildCloneSlotRows()
        {
            StackPanel panel = (StackPanel)ui["CloneSlotRolePanel"];
            panel.Children.Clear();
            string mode = cfg.GetString("clones", "roleMode", "random");
            bool visible = string.Equals(mode, "slots", StringComparison.OrdinalIgnoreCase);
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) return;

            int cap = engine.CloneCap();
            int total = cap > 0 ? cap : 7;
            if (total < 2) total = 2;
            if (total > Engine.SafeCloneLimit) total = Engine.SafeCloneLimit;
            for (int slot = 1; slot < total; slot++)
            {
                DockPanel row = new DockPanel();
                row.Margin = new Thickness(0, 0, 0, 4);
                TextBlock label = new TextBlock();
                label.Text = "第 " + (slot + 1).ToString(CultureInfo.InvariantCulture) + " 只";
                label.Width = 58;
                label.FontSize = 10;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.Foreground = new SolidColorBrush(cMuted);
                row.Children.Add(label);
                Button button = new Button();
                button.Width = 210;
                AutomationProperties.SetAutomationId(
                    button, "CloneSlotRole" + slot.ToString(CultureInfo.InvariantCulture));
                button.Style = (Style)root.Resources["Mini"];
                button.HorizontalContentAlignment = HorizontalAlignment.Left;
                button.Padding = new Thickness(9, 0, 9, 0);
                button.Content = CloneSlotRoleLabel(slot) + "  ▾";
                button.ToolTip = "第 " + (slot + 1).ToString(CultureInfo.InvariantCulture) + " 只分身的外观";
                int chosenSlot = slot;
                button.Click += delegate { ShowCloneSlotRolePopup(button, chosenSlot); };
                row.Children.Add(button);
                panel.Children.Add(row);
            }
        }

        private void ShowCloneSlotRolePopup(Button button, int slot)
        {
            Popup popup = new Popup();
            popup.AllowsTransparency = true;
            popup.StaysOpen = false;
            StackPanel list = new StackPanel();
            list.Width = Math.Max(210, button.ActualWidth);
            Border card = new Border();
            card.Background = new SolidColorBrush(cRow);
            card.BorderBrush = new SolidColorBrush(cBorder);
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(10);
            card.Padding = new Thickness(4);
            card.Child = ScrollableOptions(list, 280);
            popup.Child = card;

            string current = CloneSlotRole(slot);
            List<string> roles = new List<string> { "@current", "" };
            List<string> labels = new List<string> { "跟随主宠", "内置小鲸鱼" };
            foreach (string role in AppPaths.RoleFiles())
            {
                PetPack pack = PetPack.TryLoad(cfg, role);
                labels.Add(pack == null || string.IsNullOrEmpty(pack.Name)
                    ? System.IO.Path.GetFileNameWithoutExtension(role)
                    : PublicPackName(pack.Name));
                roles.Add(role);
            }
            Button selectedItem = null;
            for (int i = 0; i < roles.Count; i++)
            {
                string role = roles[i];
                bool selected = string.Equals(current, role, StringComparison.Ordinal);
                Button item = new Button();
                item.Style = (Style)root.Resources["Mini"];
                item.Padding = new Thickness(10, 0, 10, 0);
                item.HorizontalContentAlignment = HorizontalAlignment.Left;
                item.Content = (selected ? "● " : "   ") + labels[i];
                if (selected) selectedItem = item;
                item.Click += delegate
                {
                    Dictionary<string, object> selectedRoles = cfg.GetObject("", "cloneSlots");
                    selectedRoles[slot.ToString(CultureInfo.InvariantCulture)] = role;
                    cfg.Set("", "cloneSlots", selectedRoles);
                    cfg.Save();
                    string actual = string.Equals(role, "@current", StringComparison.OrdinalIgnoreCase)
                        ? cfg.GetString("", "role", "")
                        : role;
                    foreach (PetWindow pet in engine.Pets)
                    {
                        if (pet.Slot != slot) continue;
                        pet.ApplyRole(actual);
                        pet.ResizePet();
                    }
                    popup.IsOpen = false;
                    Update();
                };
                list.Children.Add(item);
            }
            popup.PlacementTarget = button;
            popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            popup.IsOpen = true;
            BringSelectedOptionIntoView(popup, selectedItem);
        }

        private string CloneRoleLabel()
        {
            string mode = cfg.GetString("clones", "roleMode", "random");
            if (string.Equals(mode, "current", StringComparison.OrdinalIgnoreCase)) return "跟随主宠";
            if (string.Equals(mode, "slots", StringComparison.OrdinalIgnoreCase)) return "每只自选";
            if (!string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase)) return "随机";
            string role = cfg.GetString("clones", "role", "");
            if (string.IsNullOrEmpty(role)) return "内置小鲸鱼";
            PetPack pack = PetPack.TryLoad(cfg, role);
            return pack == null || string.IsNullOrEmpty(pack.Name)
                ? System.IO.Path.GetFileNameWithoutExtension(role)
                : PublicPackName(pack.Name);
        }

        private void WireCloneRoleDropdown()
        {
            Button button = (Button)ui["CloneRoleBtn"];
            button.Click += delegate
            {
                Popup popup = new Popup();
                popup.AllowsTransparency = true;
                popup.StaysOpen = false;
                StackPanel list = new StackPanel();
                list.Width = Math.Max(210, button.ActualWidth);
                Border card = new Border();
                card.Background = new SolidColorBrush(cRow);
                card.BorderBrush = new SolidColorBrush(cBorder);
                card.BorderThickness = new Thickness(1);
                card.CornerRadius = new CornerRadius(10);
                card.Padding = new Thickness(4);
                card.Child = ScrollableOptions(list, 280);
                popup.Child = card;

                string currentMode = cfg.GetString("clones", "roleMode", "random");
                string currentRole = cfg.GetString("clones", "role", "");
                List<string> modes = new List<string> { "random", "current", "slots", "manual" };
                List<string> roles = new List<string> { "", "", "", "" };
                List<string> labels = new List<string> { "随机", "跟随主宠", "每只自选", "内置小鲸鱼" };
                foreach (string role in AppPaths.RoleFiles())
                {
                    PetPack pack = PetPack.TryLoad(cfg, role);
                    string label = pack == null || string.IsNullOrEmpty(pack.Name)
                        ? System.IO.Path.GetFileNameWithoutExtension(role)
                        : PublicPackName(pack.Name);
                    modes.Add("manual");
                    roles.Add(role);
                    labels.Add(label);
                }
                Button selectedItem = null;
                for (int i = 0; i < labels.Count; i++)
                {
                    string mode = modes[i];
                    string role = roles[i];
                    bool selected = string.Equals(currentMode, mode, StringComparison.OrdinalIgnoreCase)
                        && (!string.Equals(mode, "manual", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(currentRole, role, StringComparison.Ordinal));
                    Button item = new Button();
                    item.Style = (Style)root.Resources["Mini"];
                    item.Padding = new Thickness(10, 0, 10, 0);
                    item.HorizontalContentAlignment = HorizontalAlignment.Left;
                    item.Content = (selected ? "● " : "   ") + labels[i];
                    if (selected) selectedItem = item;
                    item.Click += delegate
                    {
                        cfg.Set("clones", "roleMode", mode);
                        cfg.Set("clones", "role", role);
                        cfg.Save();
                        popup.IsOpen = false;
                        Update();
                    };
                    list.Children.Add(item);
                }
                popup.PlacementTarget = button;
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                popup.IsOpen = true;
                BringSelectedOptionIntoView(popup, selectedItem);
            };
        }

        /// <summary>下拉：点按钮弹出选项列表，选中即写入配置并刷新。</summary>
        private void WireThemeDropdown(string buttonName, string[] values, string[] labels,
            string path, bool refresh)
        {
            Button button = (Button)ui[buttonName];
            button.Click += delegate
            {
                Popup popup = new Popup();
                popup.AllowsTransparency = true;
                popup.StaysOpen = false;
                StackPanel list = new StackPanel();
                list.Width = Math.Max(172, button.ActualWidth);
                Border card = new Border();
                card.Background = new SolidColorBrush(cRow);
                card.BorderBrush = new SolidColorBrush(cBorder);
                card.BorderThickness = new Thickness(1);
                card.CornerRadius = new CornerRadius(11);
                card.Padding = new Thickness(5);
                card.Child = ScrollableOptions(list, 320);
                popup.Child = card;
                string current = cfg.GetStringAt(path, "");
                for (int i = 0; i < values.Length; i++)
                {
                    string value = values[i];
                    Button item = new Button();
                    item.Style = (Style)root.Resources["Mini"];
                    item.Height = 34;
                    item.Margin = new Thickness(0, 1, 0, 1);
                    item.Padding = new Thickness(7, 0, 7, 0);
                    item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    item.Content = BuildThemeSelectorVisual(labels[i], value, path == "bubble.theme");
                    string chosen = value;
                    item.Click += delegate
                    {
                        cfg.SetAt(path, chosen);
                        if (path == "bubble.theme") cfg.Set("bubble", "style", "");
                        cfg.Save();
                        popup.IsOpen = false;
                        if (path == "bubble.theme")
                            engine.RefreshBubblePresentation();
                        if (refresh) engine.Data.Refresh();
                        if (path == "ui.theme")
                        {
                            RebuildForAccent(true);
                            return;
                        }
                        Update();
                    };
                    list.Children.Add(item);
                }
                popup.PlacementTarget = button;
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                popup.IsOpen = true;
            };
        }

        private FrameworkElement BuildThemeSelectorVisual(string label, string value, bool bubbleTheme)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border swatch = new Border();
            swatch.Width = 13;
            swatch.Height = 13;
            swatch.CornerRadius = new CornerRadius(7);
            swatch.Background = ThemeSwatchBrush(value);
            swatch.BorderBrush = value == "light"
                ? new SolidColorBrush(cBorder)
                : new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
            swatch.BorderThickness = new Thickness(1);
            swatch.VerticalAlignment = VerticalAlignment.Center;
            swatch.Margin = new Thickness(0, 0, 7, 0);
            row.Children.Add(swatch);
            TextBlock text = new TextBlock();
            text.Text = label;
            text.FontSize = 10.4;
            text.Foreground = new SolidColorBrush(cFg2);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextTrimming = TextTrimming.None;
            text.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            TextBlock glyph = OptionGlyph("\uE70D");
            glyph.FontSize = 9;
            glyph.Foreground = new SolidColorBrush(cMuted);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(glyph, 2);
            row.Children.Add(glyph);
            return row;
        }

        private Brush ThemeSwatchBrush(string value)
        {
            if (value == "system")
            {
                LinearGradientBrush split = new LinearGradientBrush();
                split.StartPoint = new Point(0, 0);
                split.EndPoint = new Point(1, 1);
                split.GradientStops.Add(new GradientStop(Colors.White, 0));
                split.GradientStops.Add(new GradientStop(Colors.White, 0.48));
                split.GradientStops.Add(new GradientStop(Parse("#182033"), 0.52));
                split.GradientStops.Add(new GradientStop(Parse("#182033"), 1));
                return split;
            }
            switch (value)
            {
                case "dim": return new SolidColorBrush(Parse("#252B3B"));
                case "dark": return new SolidColorBrush(Parse("#182033"));
                case "glass": return new SolidColorBrush(Parse("#D7E6FF"));
                case "frost": return new SolidColorBrush(Parse("#E8EFFF"));
                case "monet": return new SolidColorBrush(Parse("#A98B76"));
                default: return new SolidColorBrush(Parse("#FFFFFF"));
            }
        }

        /// <summary>下拉：点按钮弹出选项列表，选中即写入配置并刷新。</summary>
        private void WireDropdown(string buttonName, string[] values, string[] labels, string path, bool refresh)
        {
            Button button = (Button)ui[buttonName];
            button.ToolTip = button.Content;   // 文案被裁剪时悬停可看全称
            button.Click += delegate
            {
                Popup popup = new Popup();
                popup.AllowsTransparency = true;
                popup.StaysOpen = false;
                StackPanel list = new StackPanel();
                list.Width = Math.Max(150, button.ActualWidth);
                Border card = new Border();
                card.Background = new SolidColorBrush(cRow);
                card.BorderBrush = Brush("#39456B");
                card.BorderThickness = new Thickness(1);
                card.CornerRadius = new CornerRadius(10);
                card.Padding = new Thickness(4);
                card.Child = ScrollableOptions(list, 320);
                popup.Child = card;
                string current = cfg.GetStringAt(path, "");
                foreach (string value in values)
                {
                    int index = Array.IndexOf(values, value);
                    Button item = new Button();
                    item.Style = (Style)root.Resources["Mini"];
                    item.Padding = new Thickness(10, 0, 10, 0);
                    item.HorizontalContentAlignment = HorizontalAlignment.Left;
                    item.Content = (value == current ? "● " : "   ") + labels[index];
                    string chosen = value;
                    item.Click += delegate
                    {
                        cfg.SetAt(path, chosen);
                        if (path == "bubble.theme") cfg.Set("bubble", "style", "");
                        if (path == "bubble.effect") cfg.Set("bubble", "accent", "");
                        cfg.Save();
                        popup.IsOpen = false;
                        if (path.StartsWith("bubble.", StringComparison.Ordinal))
                            engine.RefreshBubblePresentation();
                        if (refresh) engine.Data.Refresh();
                        if (path == "ui.theme")
                        {
                            // 设置卡片自己的主题，和气泡主题互不影响。
                            RebuildForAccent(true);
                            return;
                        }
                        Update();
                    };
                    list.Children.Add(item);
                }
                popup.PlacementTarget = button;
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                popup.IsOpen = true;
            };
        }

        private void WireBubblePresetDropdown()
        {
            Button button = (Button)ui["BubblePresetBtn"];
            button.Click += delegate
            {
                Popup popup = new Popup();
                popup.AllowsTransparency = true;
                popup.StaysOpen = false;
                WrapPanel list = new WrapPanel();
                list.Width = 340;
                Border panel = new Border();
                panel.Background = new SolidColorBrush(cRow);
                panel.BorderBrush = new SolidColorBrush(cBorder);
                panel.BorderThickness = new Thickness(1);
                panel.CornerRadius = new CornerRadius(12);
                panel.Padding = new Thickness(6);
                panel.Child = list;
                popup.Child = panel;

                BubbleStylePreset current = BubbleLayouts.MatchingPreset(cfg);
                foreach (BubbleStylePreset preset in BubbleLayouts.Presets())
                {
                    Button item = new Button();
                    item.Style = (Style)root.Resources["Mini"];
                    item.Width = 160;
                    item.Height = 64;
                    item.Margin = new Thickness(3);
                    item.Padding = new Thickness(6);
                    item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    item.Content = BuildBubblePresetVisual(preset, current != null && current.Id == preset.Id);
                    item.ToolTip = preset.Label + " · " + ThemeLabelForValue(preset.Theme)
                        + " · " + EffectLabelForValue(preset.Effect);
                    BubbleStylePreset chosen = preset;
                    item.Click += delegate
                    {
                        cfg.Set("bubble", "layout", chosen.Layout);
                        cfg.Set("bubble", "theme", chosen.Theme);
                        cfg.Set("bubble", "style", "");
                        cfg.Set("bubble", "effect", chosen.Effect);
                        cfg.Set("bubble", "accent", "");
                        cfg.SetAt("bubble.radius", chosen.Radius);
                        cfg.Save();
                        popup.IsOpen = false;
                        engine.RefreshBubblePresentation();
                        engine.Data.Refresh();
                        Update();
                    };
                    list.Children.Add(item);
                }
                popup.PlacementTarget = button;
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                popup.IsOpen = true;
            };
        }

        private void WireBubbleLayoutDropdown()
        {
            Button button = (Button)ui["BubbleLayoutBtn"];
            button.Click += delegate
            {
                Popup popup = new Popup();
                popup.AllowsTransparency = true;
                popup.StaysOpen = false;
                WrapPanel list = new WrapPanel();
                list.Width = 340;
                Border panel = new Border();
                panel.Background = new SolidColorBrush(cRow);
                panel.BorderBrush = new SolidColorBrush(cBorder);
                panel.BorderThickness = new Thickness(1);
                panel.CornerRadius = new CornerRadius(12);
                panel.Padding = new Thickness(6);
                panel.Child = list;
                popup.Child = panel;

                string current = cfg.GetString("bubble", "layout", "standard");
                foreach (string value in BubbleLayoutValues)
                {
                    Button item = new Button();
                    item.Style = (Style)root.Resources["Mini"];
                    item.Width = 160;
                    item.Height = 64;
                    item.Margin = new Thickness(3);
                    item.Padding = new Thickness(6);
                    item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    item.Content = BuildBubbleLayoutVisual(value, value == current);
                    item.ToolTip = BubbleLayoutLabel(value) + " · " + BubbleLayoutDescription(value);
                    string chosen = value;
                    item.Click += delegate
                    {
                        cfg.Set("bubble", "layout", chosen);
                        cfg.Set("bubble", "style", "");
                        cfg.Save();
                        popup.IsOpen = false;
                        engine.RefreshBubblePresentation();
                        engine.Data.Refresh();
                        Update();
                    };
                    list.Children.Add(item);
                }
                popup.PlacementTarget = button;
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                popup.IsOpen = true;
            };
        }

        private string DropLabel(string[] values, string[] labels, string path, string fallback)
        {
            string current = cfg.GetStringAt(path, fallback);
            int index = Array.IndexOf(values, current);
            return index < 0 ? fallback : labels[index];
        }


        private static string EffectLabel(string effect)
        {
            switch (effect)
            {
                case "breathe": return "特效：呼吸";
                case "duo": return "特效：双色";
                case "flow": return "特效：流光";
                case "rainbow": return "特效：彩虹";
                case "orbit": return "特效：环游";
                case "shimmer": return "特效：流光闪";
                default: return "特效：关";
            }
        }

        /// <summary>控件主色：只显示当前颜色（对勾 + 光晕），点色块或按钮都能换色。</summary>
        private void BuildColorPalette(string panelName, string path, string fallback)
        {
            WrapPanel panel = (WrapPanel)ui[panelName];
            panel.Children.Clear();
            string hex = cfg.GetStringAt(path, fallback);
            if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#", StringComparison.Ordinal)) hex = fallback;
            Color fill = Parse(hex);
            Color ink = InkFor(fill);

            themeSwatchBox = new Grid();
            themeSwatchBox.Width = 34;
            themeSwatchBox.Height = 34;
            themeSwatchBox.Margin = new Thickness(0, 0, 4, 4);
            themeSwatchBox.Cursor = System.Windows.Input.Cursors.Hand;
            themeSwatchBox.ToolTip = "当前控件主色 " + hex.ToUpperInvariant() + "，点一下打开调色盘";

            themeSwatch = new Border();
            themeSwatch.Width = 30;
            themeSwatch.Height = 30;
            themeSwatch.CornerRadius = new CornerRadius(10);
            themeSwatch.Background = new SolidColorBrush(fill);
            themeSwatch.BorderBrush = new SolidColorBrush(ink);
            themeSwatch.BorderThickness = new Thickness(2);
            DropShadowEffect glow = new DropShadowEffect();
            glow.ShadowDepth = 0;
            glow.BlurRadius = 14;
            glow.Opacity = 0.55;
            glow.Color = fill;
            themeSwatch.Effect = glow;
            themeSwatchBox.Children.Add(themeSwatch);

            themeSwatchCheck = new Path();
            themeSwatchCheck.Data = Geometry.Parse("M 11,17.5 L 15.5,22 L 23.5,12.5");
            themeSwatchCheck.Stroke = new SolidColorBrush(ink);
            themeSwatchCheck.StrokeThickness = 2.4;
            themeSwatchCheck.StrokeStartLineCap = PenLineCap.Round;
            themeSwatchCheck.StrokeEndLineCap = PenLineCap.Round;
            themeSwatchCheck.StrokeLineJoin = PenLineJoin.Round;
            themeSwatchCheck.IsHitTestVisible = false;
            themeSwatchBox.Children.Add(themeSwatchCheck);

            themeSwatchBox.MouseLeftButtonUp += delegate { OpenThemePicker(themeSwatchBox); };
            panel.Children.Add(themeSwatchBox);

            themeSwatchHex = new TextBlock();
            themeSwatchHex.Text = hex.ToUpperInvariant();
            themeSwatchHex.FontSize = 11;
            themeSwatchHex.Foreground = new SolidColorBrush(cMuted);
            themeSwatchHex.VerticalAlignment = VerticalAlignment.Center;
            themeSwatchHex.Margin = new Thickness(2, 0, 8, 4);
            panel.Children.Add(themeSwatchHex);
        }

        /// <summary>控件主色：气泡强调色 + 设置卡片控件色一起换，副色自动配一个。</summary>
        private void OpenThemePicker(FrameworkElement anchor)
        {
            Pinned = true;   // 选色期间不要自动隐藏面板
            string initial = cfg.GetStringAt("bubble.color1", "#4B6BFB");
            ShowColorPicker(anchor, initial, delegate(string hex)
            {
                Pinned = true;
                cfg.SetAt("bubble.color1", hex);
                cfg.SetAt("bubble.color2", ShiftHue(hex, 85));
                cfg.Set("bubble", "accent", "");
                cfg.Save();
                engine.RefreshBubblePresentation();
                engine.Data.Refresh();
                RebuildForAccent(true);
            });
        }

        /// <summary>控件主色会注入卡片模板，改色后重建面板让高亮色立即生效。</summary>
        /// <summary>
        /// 面板只读取 ui.theme 生成自身深浅配色；气泡主色只用于设置卡片的强调色，
        /// 不会把气泡的深浅主题带回界面。
        /// </summary>
        private string PanelXaml(string accent)
        {
            string theme = cfg.GetString("ui", "theme", "system");
            ThemePalette palette = BubbleTheme.Resolve(theme, cfg);
            bool dark = palette.Dark;
            string card = dark ? "#F2171C2B" : "#F7FFFFFF";
            string cardBorder = dark ? "#2EFFFFFF" : "#1E2C3A6E";
            if (string.Equals(theme, "dim", StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
            {
                card = ToHexArgb(palette.Card);
                cardBorder = ToHexArgb(palette.Border);
            }
            if (theme == "glass") card = ToHexArgb(palette.Card);
            if (theme == "frost") card = "#F0F5FFFF";
            if (theme == "monet") card = ToHexArgb(palette.Card);
            string row = dark ? "#1B2236" : "#F1F4FA";
            string field = dark ? "#121828" : "#FFFFFF";
            string track = dark ? "#2A3350" : "#E3E8F3";
            string button = dark ? "#252D48" : "#EDF0F8";
            string buttonHover = dark ? "#33406A" : "#E1E6F4";
            string border = dark ? "#39456B" : "#D9DFEC";
            string chip = dark ? "#202A42" : "#E9EDF7";
            string nav = dark ? "#171D2B" : "#F1F4FA";
            string desk = dark ? "#0F1421" : "#E7EBF5";
            if (string.Equals(theme, "dim", StringComparison.OrdinalIgnoreCase))
            {
                row = "#2A3143";
                field = "#202637";
                track = "#3A435E";
                button = "#323B55";
                buttonHover = "#3D4865";
                border = "#4B5673";
                chip = "#303950";
                nav = "#202637";
                desk = "#181D2A";
            }
            if (theme == "monet")
            {
                row = ToHex(dark ? palette.QuipBackground : palette.BarEmpty);
                field = dark ? "#171D2B" : "#FFFFFF";
                track = ToHex(dark ? palette.Bar : palette.BarEmpty);
                button = ToHex(dark ? palette.QuipBackground : palette.QuipBackground);
                buttonHover = ToHex(palette.Bar);
                border = ToHex(palette.Border);
                chip = ToHex(palette.BarEmpty);
                nav = ToHex(dark ? palette.Card : palette.BarEmpty);
                desk = ToHex(dark ? palette.Card : palette.BarEmpty);
            }
            if (theme == "glass")
            {
                cardBorder = ToHexArgb(palette.Border);
                row = ToHexArgb(palette.QuipBackground);
                field = "#99FFFFFF";
                border = ToHexArgb(palette.Border);
                chip = ToHexArgb(AlphaColor(palette.Card, (byte)(palette.Card.A * 0.55)));
                nav = ToHexArgb(AlphaColor(palette.Card, (byte)Math.Min(255, palette.Card.A * 0.82)));
            }
            if (theme == "frost") nav = "#D9FFFFFF";
            if (theme == "glass") desk = "#33708AB0";
            if (theme == "frost") desk = "#66D9E2F5";
            cRow = Parse(nav);
            cField = Parse(field);
            cBorder = Parse(border);
            cFg = Parse(ToHex(palette.Title));
            cFg2 = Parse(ToHex(palette.RowValue));
            cMuted = Parse(ToHex(palette.Muted));
            cChip = Parse(chip);
            cCard = Parse(card);
            cOk = Parse(dark ? "#7ADFA0" : "#1F8A4C");
            cErr = Parse(dark ? "#FF9C93" : "#C93A34");
            // 设置面板的文字色也可以逐项覆盖：ui.colors.fg / fg2 / muted / ok / err
            cFg = ParseOr("ui.colors.fg", cFg);
            cFg2 = ParseOr("ui.colors.fg2", cFg2);
            cMuted = ParseOr("ui.colors.muted", cMuted);
            cOk = ParseOr("ui.colors.ok", cOk);
            cErr = ParseOr("ui.colors.err", cErr);
            return Xaml
                .Replace("__NAV__", nav)
                .Replace("__DESK__", desk)
                .Replace("__CARD__", card)
                .Replace("__CARD_BORDER__", cardBorder)
                .Replace("__FG2__", ToHex(palette.Dark ? palette.RowValue : palette.Title))
                .Replace("__FG__", ToHex(palette.Title))
                .Replace("__MUTED__", ToHex(palette.Muted))
                .Replace("__FGLABEL__", ToHex(palette.Dark ? palette.RowValue : palette.Title))
                .Replace("__ROW__", row)
                .Replace("__BTN__", button)
                .Replace("__BTN_HOVER__", buttonHover)
                .Replace("__FIELD__", field)
                .Replace("__TRACK__", track)
                .Replace("__CHIP__", chip)
                .Replace("__BORDER__", border)
                .Replace("#4B6BFB", accent);
        }

        private static string ToHex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private static string ToHexArgb(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}",
                color.A, color.R, color.G, color.B);
        }

        private static Color AlphaColor(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static Color Parse(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.Gray;
            }
        }

        private Color ParseOr(string path, Color fallback)
        {
            string value = cfg.GetStringAt(path, "");
            if (string.IsNullOrEmpty(value) || !value.StartsWith("#", StringComparison.Ordinal)) return fallback;
            return Parse(value);
        }

        private void RebuildForAccent(bool rebuild)
        {
            if (!rebuild)
            {
                Update();
                return;
            }
            bool visible = IsVisible;
            PetWindow active = ActivePet;
            // 重建后必须保持显示：否则选色/换主题时面板会因失焦而自己消失。
            Pinned = true;
            double keptLeft = double.NaN;
            double keptTop = double.NaN;
            if (root != null)
            {
                keptLeft = root.Left;
                keptTop = root.Top;
            }
            try
            {
                if (root != null) root.Close();
            }
            catch
            {
            }
            ui.Clear();
            statusById.Clear();
            keyBoxes.Clear();
            platformTokenBoxes.Clear();
            planStatus.Clear();
            planBoxes.Clear();
            themeSwatchBox = null;
            themeSwatch = null;
            themeSwatchCheck = null;
            themeSwatchHex = null;
            previewShadowShape = null;
            previewShadowLayer = null;
            previewShadowW = -1;
            Build();
            if (!visible) return;
            // Stay where the user put the card: re-showing through ShowFor() would snap it back
            // to the default spot next to the pet.
            root.Visibility = Visibility.Visible;
            if (!double.IsNaN(keptLeft) && !double.IsNaN(keptTop))
            {
                root.Left = keptLeft;
                root.Top = keptTop;
            }
            root.Show();
            root.UpdateLayout();
            root.Activate();
            Update();
        }

        private static string ShiftHue(string hex, double degrees)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(hex);
                double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                double d = max - min;
                double h = 0;
                if (d > 0.0001)
                {
                    if (max == r) h = 60 * (((g - b) / d) % 6);
                    else if (max == g) h = 60 * ((b - r) / d + 2);
                    else h = 60 * ((r - g) / d + 4);
                }
                h = (h + degrees) % 360;
                double c = max * 0 + d;
                double x = c * (1 - Math.Abs(((h / 60.0) % 2) - 1));
                double m = max - c;
                double rr = 0, gg = 0, bb = 0;
                double segment = (h % 360 + 360) % 360 / 60.0;
                if (segment < 1) { rr = c; gg = x; }
                else if (segment < 2) { rr = x; gg = c; }
                else if (segment < 3) { gg = c; bb = x; }
                else if (segment < 4) { gg = x; bb = c; }
                else if (segment < 5) { rr = x; bb = c; }
                else { rr = c; bb = x; }
                return string.Format("#{0:X2}{1:X2}{2:X2}",
                    (byte)Math.Round((rr + m) * 255),
                    (byte)Math.Round((gg + m) * 255),
                    (byte)Math.Round((bb + m) * 255));
            }
            catch
            {
                return "#A855F7";
            }
        }

        private void MarkPalette(string panelName, string current)
        {
            if (themeSwatch == null) return;
            if (string.IsNullOrEmpty(current) || !current.StartsWith("#", StringComparison.Ordinal)) current = "#4B6BFB";
            Color fill = Parse(current);
            Color ink = InkFor(fill);
            themeSwatch.Background = new SolidColorBrush(fill);
            themeSwatch.BorderBrush = new SolidColorBrush(ink);
            if (themeSwatchCheck != null) themeSwatchCheck.Stroke = new SolidColorBrush(ink);
            DropShadowEffect glow = themeSwatch.Effect as DropShadowEffect;
            if (glow != null) glow.Color = fill;
            if (themeSwatchHex != null) themeSwatchHex.Text = current.ToUpperInvariant();
            if (themeSwatchBox != null) themeSwatchBox.ToolTip = "当前控件主色 " + current.ToUpperInvariant() + "，点一下打开调色盘";
        }

        /// <summary>深色底配白、浅色底配墨色：对勾与描边在任何主题色上都看得清。</summary>
        private static Color InkFor(Color fill)
        {
            double luma = (fill.R * 299 + fill.G * 587 + fill.B * 114) / 1000.0;
            return luma > 165 ? Color.FromRgb(0x24, 0x2C, 0x40) : Colors.White;
        }

        /// <summary>混合配色：只列出已选颜色（点色块移除），新颜色从「添加颜色…」的调色盘里取。</summary>
        private void BuildMixPalette()
        {
            MarkMixPalette();
        }

        private void ToggleMixColor(string hex)
        {
            List<string> colors = BubbleTheme.MixColors(cfg);
            if (colors.Contains(hex)) colors.Remove(hex);
            else
            {
                colors.Add(hex);
                TrimMix(colors);
            }
            cfg.SetAt("bubble.colors", colors.ToArray());
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        /// <summary>混色列表超上限时顶掉最早设置的颜色（FIFO），保证气泡不会挂一长串颜色。</summary>
        private void TrimMix(List<string> colors)
        {
            int max = (int)cfg.GetNumberAt("bubble.mixMax", 6);
            if (max < 2) max = 2;
            if (max > 12) max = 12;
            while (colors.Count > max) colors.RemoveAt(0);
        }

        private void MarkMixPalette()
        {
            WrapPanel panel = (WrapPanel)ui["MixPanel"];
            panel.Children.Clear();
            List<string> colors = BubbleTheme.MixColors(cfg);
            if (colors.Count == 0)
            {
                TextBlock empty = new TextBlock();
                empty.Text = "还没有混色，点右边从调色盘里挑";
                empty.FontSize = 11;
                empty.Foreground = new SolidColorBrush(cMuted);
                empty.VerticalAlignment = VerticalAlignment.Center;
                empty.Margin = new Thickness(0, 0, 6, 4);
                panel.Children.Add(empty);
                return;
            }
            foreach (string hex in colors)
            {
                panel.Children.Add(MixSwatch(hex));
            }
        }

        /// <summary>已选混色的色块：右上角小叉=点一下移除，悬停发光提示可点。</summary>
        private FrameworkElement MixSwatch(string hex)
        {
            Color fill = Parse(hex);
            Color ink = InkFor(fill);
            Grid wrap = new Grid();
            wrap.Width = 34;
            wrap.Height = 34;
            wrap.Margin = new Thickness(0, 0, 2, 4);
            wrap.Cursor = System.Windows.Input.Cursors.Hand;
            wrap.ToolTip = hex.ToUpperInvariant() + " · 点一下移除";

            Border block = new Border();
            block.Width = 28;
            block.Height = 28;
            block.CornerRadius = new CornerRadius(9);
            block.Background = new SolidColorBrush(fill);
            block.BorderBrush = new SolidColorBrush(Color.FromArgb(110, ink.R, ink.G, ink.B));
            block.BorderThickness = new Thickness(1);
            block.HorizontalAlignment = HorizontalAlignment.Left;
            block.VerticalAlignment = VerticalAlignment.Bottom;
            wrap.Children.Add(block);

            Border badge = new Border();
            badge.Width = 15;
            badge.Height = 15;
            badge.CornerRadius = new CornerRadius(8);
            badge.Background = new SolidColorBrush(ink);
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            badge.VerticalAlignment = VerticalAlignment.Top;
            Canvas cross = new Canvas();
            cross.Width = 9;
            cross.Height = 9;
            Path crossLine = new Path();
            crossLine.Data = Geometry.Parse("M 1.5,1.5 L 7.5,7.5 M 7.5,1.5 L 1.5,7.5");
            crossLine.Stroke = new SolidColorBrush(fill);
            crossLine.StrokeThickness = 1.6;
            crossLine.StrokeStartLineCap = PenLineCap.Round;
            crossLine.StrokeEndLineCap = PenLineCap.Round;
            cross.Children.Add(crossLine);
            badge.Child = cross;
            wrap.Children.Add(badge);

            wrap.MouseEnter += delegate
            {
                DropShadowEffect glow = new DropShadowEffect();
                glow.ShadowDepth = 0;
                glow.BlurRadius = 12;
                glow.Opacity = 0.6;
                glow.Color = fill;
                block.Effect = glow;
            };
            wrap.MouseLeave += delegate { block.Effect = null; };
            wrap.MouseLeftButtonUp += delegate { ToggleMixColor(hex); };
            return wrap;
        }

        private static System.Drawing.Color ToDrawing(Color color)
        {
            return System.Drawing.Color.FromArgb(color.R, color.G, color.B);
        }

        // ------------------------------------------------------------ 内嵌调色盘（色相条 + SV 方块 + hex）
        private void ShowColorPicker(FrameworkElement anchor, string initialHex, Action<string> apply)
        {
            ShowColorPicker(anchor, initialHex, apply, false);
        }

        /// <summary>withMixCap = 混色调色盘：底部多一条「混色上限」，改小立刻让多余的旧色让位。</summary>
        private void ShowColorPicker(FrameworkElement anchor, string initialHex, Action<string> apply, bool withMixCap)
        {
            Color initial = Parse(string.IsNullOrEmpty(initialHex) ? "#4B6BFB" : initialHex);
            double hue, sat, val;
            ToHsv(initial, out hue, out sat, out val);

            Popup popup = new Popup();
            popup.AllowsTransparency = true;
            popup.StaysOpen = false;
            Canvas square = new Canvas();
            square.Width = 200;
            square.Height = 132;
            Rectangle sv = new Rectangle();
            sv.Width = 200;
            sv.Height = 132;
            sv.Fill = BuildSvBrush(hue);
            square.Children.Add(sv);
            Ellipse cursor = new Ellipse();
            cursor.Width = 12;
            cursor.Height = 12;
            cursor.Stroke = Brushes.White;
            cursor.StrokeThickness = 2;
            cursor.IsHitTestVisible = false;
            square.Children.Add(cursor);

            Canvas strip = new Canvas();
            strip.Width = 18;
            strip.Height = 132;
            Rectangle hues = new Rectangle();
            hues.Width = 18;
            hues.Height = 132;
            LinearGradientBrush hueBrush = new LinearGradientBrush();
            hueBrush.StartPoint = new Point(0, 0);
            hueBrush.EndPoint = new Point(0, 1);
            for (int i = 0; i <= 6; i++)
            {
                hueBrush.GradientStops.Add(new GradientStop(HueColor(i * 60), i / 6.0));
            }
            hues.Fill = hueBrush;
            strip.Children.Add(hues);
            Ellipse hueMark = new Ellipse();
            hueMark.Width = 12;
            hueMark.Height = 12;
            hueMark.Stroke = Brushes.White;
            hueMark.StrokeThickness = 2;
            hueMark.IsHitTestVisible = false;
            strip.Children.Add(hueMark);

            Border preview = new Border();
            preview.Width = 34;
            preview.Height = 26;
            preview.CornerRadius = new CornerRadius(7);
            TextBox hexBox = new TextBox();
            hexBox.Style = (Style)root.Resources["TxtBox"];
            hexBox.Width = 92;
            TextBlock caption = new TextBlock();
            caption.Text = "拖动选色，或直接改 hex；下方预置一键取色";
            caption.FontSize = 10;
            caption.Foreground = new SolidColorBrush(cMuted);
            caption.Margin = new Thickness(0, 6, 0, 0);

            StackPanel right = new StackPanel();
            right.Margin = new Thickness(12, 0, 0, 0);
            right.Children.Add(preview);
            right.Children.Add(hexBox);
            StackPanel body = new StackPanel();
            body.Orientation = Orientation.Horizontal;
            body.Children.Add(square);
            body.Children.Add(strip);
            body.Children.Add(right);
            StackPanel card = new StackPanel();
            card.Children.Add(body);
            card.Children.Add(caption);
            Border frame = new Border();
            frame.Background = new SolidColorBrush(cRow);
            frame.BorderBrush = new SolidColorBrush(cBorder);
            frame.BorderThickness = new Thickness(1);
            frame.CornerRadius = new CornerRadius(12);
            frame.Padding = new Thickness(12);
            frame.Child = card;
            popup.Child = frame;
            popup.PlacementTarget = anchor;
            popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            List<Border> presetDots = new List<Border>();
            Action refresh = null;
            refresh = delegate
            {
                Color current = FromHsv(hue, sat, val);
                sv.Fill = BuildSvBrush(hue);
                Canvas.SetLeft(cursor, sat * 194 + 3);
                Canvas.SetTop(cursor, (1 - val) * 126 + 3);
                Canvas.SetTop(hueMark, hue / 360.0 * 120 + 6);
                preview.Background = new SolidColorBrush(current);
                string shownHex = string.Format("#{0:X2}{1:X2}{2:X2}", current.R, current.G, current.B);
                if (!hexBox.IsFocused)
                {
                    hexBox.Text = shownHex;
                }
                // 预置色里命中当前色的那格描白圈：选中态一眼可见。
                for (int i = 0; i < presetDots.Count; i++)
                {
                    bool on = string.Equals(Convert.ToString(presetDots[i].Tag), shownHex, StringComparison.OrdinalIgnoreCase);
                    presetDots[i].BorderBrush = on ? Brushes.White : Brushes.Transparent;
                }
            };
            // 预置色收进调色盘底部：主界面不再挂一排预设色块，这里点一下即取色。
            WrapPanel presets = new WrapPanel();
            presets.Margin = new Thickness(0, 2, 0, 0);
            foreach (string presetHex in ColorPresets)
            {
                Border dot = new Border();
                dot.Width = 20;
                dot.Height = 20;
                dot.CornerRadius = new CornerRadius(7);
                dot.Margin = new Thickness(0, 6, 6, 0);
                dot.Cursor = System.Windows.Input.Cursors.Hand;
                dot.Tag = presetHex;
                dot.Background = Brush(presetHex);
                dot.BorderBrush = Brushes.Transparent;
                dot.BorderThickness = new Thickness(2);
                dot.ToolTip = presetHex;
                string presetPick = presetHex;
                dot.MouseLeftButtonUp += delegate
                {
                    ToHsv(Parse(presetPick), out hue, out sat, out val);
                    refresh();
                    apply(presetPick);
                };
                presetDots.Add(dot);
                presets.Children.Add(dot);
            }
            card.Children.Add(presets);
            if (withMixCap) card.Children.Add(BuildMixCapRow());
            bool draggingSquare = false;
            bool draggingHue = false;
            System.Windows.Input.MouseButtonEventHandler down = delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                Point point = e.GetPosition(sender as IInputElement);
                if (ReferenceEquals(sender, square)) { draggingSquare = true; ApplySquare(point, ref hue, ref sat, ref val); }
                else { draggingHue = true; hue = Math.Max(0, Math.Min(359.9, point.Y / 132.0 * 360)); }
                refresh();
            };
            System.Windows.Input.MouseEventHandler move = delegate(object sender, System.Windows.Input.MouseEventArgs e)
            {
                if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) { draggingSquare = false; draggingHue = false; return; }
                Point point = e.GetPosition(sender as IInputElement);
                if (draggingSquare) ApplySquare(point, ref hue, ref sat, ref val);
                else if (draggingHue) hue = Math.Max(0, Math.Min(359.9, point.Y / 132.0 * 360));
                else return;
                refresh();
            };
            square.MouseLeftButtonDown += down;
            square.MouseMove += move;
            strip.MouseLeftButtonDown += down;
            strip.MouseMove += move;
            hexBox.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key != System.Windows.Input.Key.Enter) return;
                string text = hexBox.Text.Trim();
                if (!text.StartsWith("#", StringComparison.Ordinal)) text = "#" + text;
                ToHsv(Parse(text), out hue, out sat, out val);
                refresh();
            };
            System.Windows.Input.MouseButtonEventHandler up = delegate
            {
                draggingSquare = false;
                draggingHue = false;
                Color picked = FromHsv(hue, sat, val);
                apply(string.Format("#{0:X2}{1:X2}{2:X2}", picked.R, picked.G, picked.B));
            };
            square.MouseLeftButtonUp += up;
            strip.MouseLeftButtonUp += up;
            refresh();
            popup.IsOpen = true;
        }

        /// <summary>混色调色盘底部的「混色上限」：加满之后新颜色顶掉最早设置的，不会越加越长。</summary>
        private FrameworkElement BuildMixCapRow()
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Margin = new Thickness(0, 8, 0, 0);
            TextBlock label = new TextBlock();
            label.Text = "混色上限";
            label.FontSize = 10;
            label.Foreground = new SolidColorBrush(cMuted);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(label);
            Slider slider = new Slider();
            slider.Style = (Style)root.Resources["Flat"];
            slider.Width = 130;
            slider.Minimum = 3;
            slider.Maximum = 12;
            slider.IsSnapToTickEnabled = true;
            slider.TickFrequency = 1;
            double saved = cfg.GetNumberAt("bubble.mixMax", 6);
            slider.Value = Math.Max(3, Math.Min(12, saved));
            TextBlock value = new TextBlock();
            value.FontSize = 10;
            value.Foreground = new SolidColorBrush(cFg2);
            value.VerticalAlignment = VerticalAlignment.Center;
            value.Margin = new Thickness(8, 0, 0, 0);
            value.Text = ((int)slider.Value).ToString(CultureInfo.InvariantCulture) + " 色";
            slider.ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                int mixMax = (int)Math.Round(e.NewValue);
                value.Text = mixMax.ToString(CultureInfo.InvariantCulture) + " 色";
                cfg.SetAt("bubble.mixMax", (double)mixMax);
                List<string> mix = BubbleTheme.MixColors(cfg);
                TrimMix(mix);
                cfg.SetAt("bubble.colors", mix.ToArray());
                cfg.Save();
                MarkMixPalette();
                engine.Data.Refresh();
            };
            row.Children.Add(slider);
            row.Children.Add(value);
            return row;
        }

        private static void ApplySquare(Point point, ref double hue, ref double sat, ref double val)
        {
            sat = Math.Max(0, Math.Min(1, point.X / 200.0));
            val = Math.Max(0, Math.Min(1, 1 - point.Y / 132.0));
        }

        private static Brush BuildSvBrush(double hue)
        {
            LinearGradientBrush white = new LinearGradientBrush(Colors.White, HueColor(hue), new Point(0, 0.5), new Point(1, 0.5));
            LinearGradientBrush black = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, new Point(0.5, 0), new Point(0.5, 1));
            Grid overlay = new Grid();
            overlay.Width = 200;
            overlay.Height = 132;
            Rectangle baseRect = new Rectangle();
            baseRect.Width = 200;
            baseRect.Height = 132;
            baseRect.Fill = white;
            overlay.Children.Add(baseRect);
            Rectangle dark = new Rectangle();
            dark.Width = 200;
            dark.Height = 132;
            dark.Fill = black;
            overlay.Children.Add(dark);
            VisualBrush brush = new VisualBrush(overlay);
            return brush;
        }

        private static Color HueColor(double degrees)
        {
            double h = (degrees % 360 + 360) % 360 / 60.0;
            double x = 1 - Math.Abs(h % 2 - 1);
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = 1; g = x; }
            else if (h < 2) { r = x; g = 1; }
            else if (h < 3) { g = 1; b = x; }
            else if (h < 4) { g = x; b = 1; }
            else if (h < 5) { r = x; b = 1; }
            else { r = 1; b = x; }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static void ToHsv(Color color, out double hue, out double sat, out double val)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            val = max;
            sat = max <= 0 ? 0 : d / max;
            if (d < 0.0001) { hue = 0; return; }
            if (max == r) hue = 60 * (((g - b) / d) % 6);
            else if (max == g) hue = 60 * ((b - r) / d + 2);
            else hue = 60 * ((r - g) / d + 4);
            if (hue < 0) hue += 360;
        }

        private static Color FromHsv(double hue, double sat, double val)
        {
            double c = val * sat;
            double h = (hue % 360 + 360) % 360 / 60.0;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double m = val - c;
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        // ------------------------------------------------------------ 实时预览（迷你气泡）
        private void UpdatePreview()
        {
            string theme = cfg.GetString("bubble", "theme", "");
            if (string.IsNullOrEmpty(theme)) theme = cfg.GetString("bubble", "style", "light");
            ThemePalette palette = BubbleTheme.Resolve(theme, cfg);
            Color previewAccent = Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB"));
            BubbleTokens tokens = BubbleTokens.Resolve(palette, previewAccent);
            Border card = (Border)ui["FxCard"];
            Border tail = (Border)ui["FxTail"];
            card.Background = new SolidColorBrush(tokens.Surface);
            card.BorderBrush = new SolidColorBrush(palette.Border);
            ((TextBlock)ui["FxTitle"]).Foreground = new SolidColorBrush(tokens.Title);
            ((TextBlock)ui["FxAmount"]).Foreground = new SolidColorBrush(tokens.Primary);
            ((TextBlock)ui["FxRow"]).Foreground = new SolidColorBrush(tokens.Secondary);
            string bubbleFont = cfg.GetString("bubble", "font", "");
            FontFamily previewFamily = string.IsNullOrEmpty(bubbleFont)
                ? SystemFonts.MessageFontFamily : new FontFamily(bubbleFont);
            double requestedScale = cfg.GetNumber("bubble", "fontScale", 1.0);
            double previewScale = Math.Sqrt(Math.Max(0.7, Math.Min(1.8, requestedScale)));
            TextBlock previewTitle = (TextBlock)ui["FxTitle"];
            TextBlock previewAmount = (TextBlock)ui["FxAmount"];
            TextBlock previewRow = (TextBlock)ui["FxRow"];
            previewTitle.FontFamily = previewFamily;
            previewAmount.FontFamily = previewFamily;
            previewRow.FontFamily = previewFamily;
            previewTitle.FontSize = 11 * previewScale;
            previewAmount.FontSize = 19 * previewScale;
            previewRow.FontSize = 10 * previewScale;
            BubbleLayoutSpec bubbleLayout = BubbleLayouts.ResolveAdaptive(cfg);
            bool customPreview = string.Equals(bubbleLayout.Id, "custom", StringComparison.Ordinal);
            ((Border)ui["FxIconTile"]).Visibility = !customPreview
                || cfg.GetFlag("bubble", "customIcon", true)
                ? Visibility.Visible : Visibility.Collapsed;
            previewTitle.Visibility = !customPreview
                || cfg.GetFlag("bubble", "customHeader", true)
                ? Visibility.Visible : Visibility.Collapsed;
            previewAmount.Visibility = !customPreview
                || cfg.GetFlag("bubble", "customHero", true)
                ? Visibility.Visible : Visibility.Collapsed;
            previewRow.Visibility = !customPreview
                || cfg.GetFlag("bubble", "customDetail", true)
                || cfg.GetFlag("bubble", "customMetrics", true)
                ? Visibility.Visible : Visibility.Collapsed;
            Visibility meterVisibility = !customPreview
                || cfg.GetFlag("bubble", "customChart", true)
                ? Visibility.Visible : Visibility.Collapsed;
            ((Border)ui["FxMeterTrack"]).Visibility = meterVisibility;
            ((Border)ui["FxMeterFill"]).Visibility = meterVisibility;
            double radius = cfg.GetNumber("bubble", "radius", 16);
            // 预览忠实反映真实圆角（不再打 0.7 折），阴影也跟着圆角走，与气泡一致。
            card.CornerRadius = new CornerRadius(Math.Max(2, Math.Min(40, radius)));
            Border iconTile = (Border)ui["FxIconTile"];
            iconTile.Background = new SolidColorBrush(Color.FromArgb(
                palette.Dark ? (byte)52 : (byte)28,
                previewAccent.R, previewAccent.G, previewAccent.B));
            iconTile.BorderBrush = new SolidColorBrush(Color.FromArgb(
                palette.Dark ? (byte)88 : (byte)48,
                previewAccent.R, previewAccent.G, previewAccent.B));
            Border meterTrack = (Border)ui["FxMeterTrack"];
            meterTrack.Background = new SolidColorBrush(Color.FromArgb(
                palette.Dark ? (byte)150 : (byte)210,
                palette.BarEmpty.R, palette.BarEmpty.G, palette.BarEmpty.B));
            Border meterFill = (Border)ui["FxMeterFill"];
            meterFill.Background = new SolidColorBrush(previewAccent);
            double meterRatio = bubbleLayout.Id == "minimal" ? 0.52
                : (bubbleLayout.Id == "compact" ? 0.68
                    : (bubbleLayout.Id == "dashboard" ? 0.94
                        : (bubbleLayout.Id == "wide" ? 0.86 : 0.78)));
            meterFill.Width = Math.Round(112 * meterRatio);
            card.Width = Math.Max(150, Math.Min(260, bubbleLayout.Width * 0.70));
            card.Height = Math.Max(92, Math.Min(156, bubbleLayout.Height * 0.66));
            card.Opacity = cfg.GetNumber("bubble", "opacity", 1.0);
            // 预览要真的"看得出"：宽度映射更宽、圆角/尾巴/阴影/字号/透明度都放大呈现。
            card.UpdateLayout();
            double tailSize = Math.Max(6, Math.Min(26, cfg.GetNumber("bubble", "tailSize", 13)));
            previewShadowW = -1;   // 尺寸/圆角标记失效，下一帧重算阴影层

            tail.Width = tailSize;
            tail.Height = tailSize;
            tail.Background = card.Background;
            tail.BorderBrush = card.BorderBrush;
            string layoutHint = bubbleLayout.Label;
            ((TextBlock)ui["FxHint"]).Text = layoutHint + " "
                + ((int)Math.Round(bubbleLayout.Width)).ToString(CultureInfo.InvariantCulture) + "×"
                + ((int)Math.Round(bubbleLayout.Height)).ToString(CultureInfo.InvariantCulture) + " · "
                + DropLabel(EffectValues, EffectLabels, "bubble.effect", "关闭")
                + " · " + DropLabel(ThemeValues, ThemeLabels, "bubble.theme", "浅色");
            ApplyPreviewBrush();
        }

        private void ApplyPreviewBrush()
        {
            List<Color> list = new List<Color>();
            foreach (string hex in BubbleTheme.MixColors(cfg)) list.Add(Parse(hex));
            if (list.Count == 0) list.Add(Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")));
            string motion = cfg.GetString("bubble", "effect", "off");
            double depth = cfg.GetNumber("bubble", "effectDepth", 1.0);
            bool reverse = cfg.GetFlag("bubble", "effectReverse", false);
            // 和气泡本体共用同一套动效算法：预览里看到的节奏 = 桌宠气泡的节奏。
            Color second = Parse(cfg.GetStringAt("bubble.color2", "#A855F7"));
            Brush brush = BubbleEffects.Brush(motion, list, list[0], second, previewPhase, depth, reverse);
            ((Ellipse)ui["FxDot"]).Fill = brush;
            ((Border)ui["FxIconTile"]).BorderBrush =
                new SolidColorBrush(Color.FromArgb(120, list[0].R, list[0].G, list[0].B));
            ((Border)ui["FxMeterFill"]).Background = brush;
            ((Border)ui["FxCard"]).BorderBrush = new SolidColorBrush(Color.FromArgb(150, list[0].R, list[0].G, list[0].B));
            // 尾巴贴在卡片底部中间。
            Border card = (Border)ui["FxCard"];
            Border tail = (Border)ui["FxTail"];
            double cardWidth = card.Width;
            double cardHeight = card.Height > 0
                ? card.Height : (card.ActualHeight > 0 ? card.ActualHeight : 66);
            Canvas previewCanvas = (Canvas)ui["FxCanvas"];
            double canvasWidth = previewCanvas.ActualWidth > 2 ? previewCanvas.ActualWidth : 272;
            double cardLeft = Math.Max(6, (canvasWidth - cardWidth) / 2);
            double cardTop = Math.Max(6, (previewCanvas.Height - cardHeight) / 2);
            Canvas.SetLeft(card, cardLeft);
            Canvas.SetTop(card, cardTop);
            Canvas.SetLeft(tail, cardLeft + cardWidth / 2 - tail.Width / 2);
            Canvas.SetTop(tail, cardTop + cardHeight - tail.Height / 2 - 2);
            bool showTail = cfg.GetFlag("bubble", "tail", true);
            tail.Visibility = showTail ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreviewShadow(cardLeft, cardTop, cardWidth, cardHeight);
        }

        /// <summary>
        /// 预览的阴影与气泡同款：单独一层 + 挖掉卡片形状的裁剪，
        /// 不然圆角拉大时四角会透出阴影色。只在尺寸/圆角变化时重算。
        /// </summary>
        private void UpdatePreviewShadow(double cardLeft, double cardTop, double cardWidth, double cardHeight)
        {
            if (previewShadowLayer == null)
            {
                previewShadowShape = new Border();
                previewShadowShape.Background = Brushes.Black;
                DropShadowEffect effect = new DropShadowEffect();
                effect.Color = Colors.Black;
                previewShadowShape.Effect = effect;
                previewShadowShape.Margin = new Thickness(PreviewShadowPad);
                previewShadowLayer = new Grid();
                previewShadowLayer.IsHitTestVisible = false;
                previewShadowLayer.Children.Add(previewShadowShape);
                ((Canvas)ui["FxCanvas"]).Children.Insert(0, previewShadowLayer);
                ((Border)ui["FxCard"]).Effect = null;   // 阴影不再挂在卡片上
            }
            double radius = Math.Max(0, Math.Min(40, cfg.GetNumberAt("bubble.radius", 16)));
            if (Math.Abs(previewShadowW - cardWidth) < 0.5 && Math.Abs(previewShadowH - cardHeight) < 0.5
                && Math.Abs(previewShadowRadius - radius) < 0.01)
            {
                return;
            }
            previewShadowW = cardWidth;
            previewShadowH = cardHeight;
            previewShadowRadius = radius;
            DropShadowEffect shadow = (DropShadowEffect)previewShadowShape.Effect;
            shadow.BlurRadius = (14 + radius * 0.5) * 0.7;
            shadow.ShadowDepth = (3 + radius * 0.15) * 0.6;
            shadow.Opacity = cfg.GetNumberAt("bubble.shadow", 0.28);
            previewShadowShape.CornerRadius = new CornerRadius(radius);
            Canvas.SetLeft(previewShadowLayer, cardLeft - PreviewShadowPad);
            Canvas.SetTop(previewShadowLayer, cardTop - PreviewShadowPad);
            previewShadowLayer.Width = cardWidth + PreviewShadowPad * 2;
            previewShadowLayer.Height = cardHeight + PreviewShadowPad * 2;
            RectangleGeometry outer = new RectangleGeometry(
                new Rect(0, 0, cardWidth + PreviewShadowPad * 2, cardHeight + PreviewShadowPad * 2));
            RectangleGeometry inner = new RectangleGeometry(
                new Rect(PreviewShadowPad, PreviewShadowPad, cardWidth, cardHeight), radius, radius);
            previewShadowLayer.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        }

        private static string ToHexString(System.Drawing.Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        // ------------------------------------------------------------ 分组折叠
        /// <summary>
        /// Turns every [Sect] heading into a collapsible group header so the card stays tidy;
        /// the open/closed state is remembered in pet-config.json (ui.sections).
        /// </summary>
        /// <summary>左侧导航：每个 [Sect] 标题变成一页，点哪页只显示那一页。</summary>
        private void BuildNavPages()
        {
            StackPanel source = (StackPanel)ui["PageSource"];
            StackPanel nav = (StackPanel)ui["Nav"];
            StackPanel host = (StackPanel)ui["Main"];
            List<UIElement> children = new List<UIElement>();
            foreach (UIElement child in source.Children) children.Add(child);
            source.Children.Clear();

            Style sectionStyle = (Style)root.Resources["Sect"];
            navButtons.Clear();
            pageOrder.Clear();
            Dictionary<string, StackPanel> rawPages = new Dictionary<string, StackPanel>();
            StackPanel current = null;
            for (int i = 0; i < children.Count; i++)
            {
                UIElement child = children[i];
                TextBlock text = child as TextBlock;
                if (text != null && ReferenceEquals(text.Style, sectionStyle))
                {
                    string title = text.Text;
                    current = new StackPanel();
                    current.HorizontalAlignment = HorizontalAlignment.Stretch;
                    current.Visibility = Visibility.Collapsed;
                    host.Children.Add(current);
                    rawPages[title] = current;
                    continue;
                }
                if (current != null) current.Children.Add(child);
            }

            List<KeyValuePair<string, string[]>> groups =
                new List<KeyValuePair<string, string[]>>();
            groups.Add(new KeyValuePair<string, string[]>(
                "主题与气泡", new string[] { "主题与气泡" }));
            groups.Add(new KeyValuePair<string, string[]>(
                "宠物外观", new string[] { "外观" }));
            groups.Add(new KeyValuePair<string, string[]>(
                "宠物设置", new string[] { "宠物显示", "动作点播", "行为", "台词与互动", "分身" }));
            groups.Add(new KeyValuePair<string, string[]>(
                "余额", new string[] { "余额接口" }));
            groups.Add(new KeyValuePair<string, string[]>(
                "套餐额度", new string[] { "套餐额度" }));
            groups.Add(new KeyValuePair<string, string[]>(
                "API 密钥", new string[0]));
            groups.Add(new KeyValuePair<string, string[]>(
                "应用更新", new string[] { "应用更新" }));

            Dictionary<string, StackPanel> navPages = new Dictionary<string, StackPanel>();
            foreach (KeyValuePair<string, string[]> group in groups)
            {
                StackPanel page = new StackPanel();
                page.HorizontalAlignment = HorizontalAlignment.Stretch;
                page.Visibility = Visibility.Collapsed;
                host.Children.Add(page);
                navPages[group.Key] = page;
                foreach (string rawTitle in group.Value)
                {
                    StackPanel raw;
                    if (!rawPages.TryGetValue(rawTitle, out raw)) continue;
                    List<UIElement> items = new List<UIElement>();
                    foreach (UIElement child in raw.Children) items.Add(child);
                    raw.Children.Clear();
                    foreach (UIElement child in items) page.Children.Add(child);
                }

                Button item = new Button();
                item.Style = (Style)root.Resources["Nav"];
                item.Width = 126;
                item.Content = BuildNavVisual(group.Key, false);
                item.ToolTip = group.Key;
                AutomationProperties.SetName(item, group.Key);
                item.Tag = page;
                item.BorderThickness = new Thickness(0);
                item.BorderBrush = Brushes.Transparent;
                Button button = item;
                item.Click += delegate { ShowPage(button, (StackPanel)button.Tag); };
                nav.Children.Add(item);
                navButtons.Add(item);
                pageOrder.Add(group.Key);
            }
            StackPanel balancePage;
            StackPanel planPage;
            StackPanel keyPage;
            if (navPages.TryGetValue("余额", out balancePage)
                && navPages.TryGetValue("套餐额度", out planPage)
                && navPages.TryGetValue("API 密钥", out keyPage))
            {
                MovePageElement(balancePage, keyPage, ui["ProviderKeyList"] as UIElement);
                MovePageElement(planPage, keyPage, ui["PlanKeyList"] as UIElement);
                MovePageElement(balancePage, keyPage, ui["ProxyRow"] as UIElement);
                MovePageElement(balancePage, keyPage, ui["ProxyHint"] as UIElement);
            }
            foreach (StackPanel raw in rawPages.Values)
            {
                if (raw.Children.Count == 0) host.Children.Remove(raw);
            }
            for (int i = 0; i < navButtons.Count; i++)
                DecorateSettingsPage((StackPanel)navButtons[i].Tag, pageOrder[i]);
            if (navButtons.Count > 0) ShowPage(navButtons[0], (StackPanel)navButtons[0].Tag);
        }

        private static void MovePageElement(Panel from, Panel to, UIElement element)
        {
            if (from == null || to == null || element == null) return;
            if (!from.Children.Contains(element)) return;
            from.Children.Remove(element);
            to.Children.Add(element);
        }

        private void ModernizeNumericControls()
        {
            numericSteppers.Clear();
            foreach (NumericControlSpec spec in NumericControlSpecs)
            {
                Slider slider = root.FindName(spec.SliderName) as Slider;
                TextBlock value = root.FindName(spec.ValueName) as TextBlock;
                Panel parent = slider == null ? null : slider.Parent as Panel;
                if (slider == null || value == null || parent == null) continue;

                int sliderIndex = parent.Children.IndexOf(slider);
                int sliderColumn = Grid.GetColumn(slider);
                int columnSpan = 1;
                Panel valueParent = value.Parent as Panel;
                if (valueParent is Grid && ReferenceEquals(valueParent, parent))
                {
                    int valueColumn = Grid.GetColumn(value);
                    if (valueColumn > sliderColumn) columnSpan = valueColumn - sliderColumn + 1;
                }
                if (valueParent != null) valueParent.Children.Remove(value);

                Border shell = new Border();
                shell.Height = 32;
                shell.CornerRadius = new CornerRadius(8);
                shell.Background = new SolidColorBrush(cField);
                shell.BorderBrush = new SolidColorBrush(cBorder);
                shell.BorderThickness = new Thickness(1);
                shell.Focusable = true;
                shell.ToolTip = spec.Label + "；点击数值后可继续用滚轮微调";
                AutomationProperties.SetName(shell, spec.Label);

                Grid inner = new Grid();
                inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                RepeatButton minus = BuildStepperButton("−", "减少" + spec.Label,
                    delegate { AdjustNumericSlider(slider, spec.Step, false); });
                RepeatButton plus = BuildStepperButton("+", "增加" + spec.Label,
                    delegate { AdjustNumericSlider(slider, spec.Step, true); });
                inner.Children.Add(minus);
                Grid.SetColumn(plus, 2);
                inner.Children.Add(plus);

                value.MinWidth = 0;
                value.Margin = new Thickness(0);
                value.HorizontalAlignment = HorizontalAlignment.Center;
                value.TextAlignment = TextAlignment.Center;
                value.FontWeight = FontWeights.SemiBold;
                value.VerticalAlignment = VerticalAlignment.Center;
                value.MouseLeftButtonDown += delegate
                {
                    shell.Focus();
                };
                Grid.SetColumn(value, 1);
                inner.Children.Add(value);
                shell.Child = inner;
                shell.IsKeyboardFocusWithinChanged += delegate
                {
                    bool active = shell.IsKeyboardFocusWithin;
                    shell.BorderBrush = new SolidColorBrush(active ? cFg2 : cBorder);
                    shell.BorderThickness = new Thickness(active ? 1.5 : 1);
                };
                shell.MouseWheel += delegate(object sender, MouseWheelEventArgs e)
                {
                    if (!shell.IsKeyboardFocusWithin) return;
                    AdjustNumericSlider(slider, spec.Step, e.Delta > 0);
                    e.Handled = true;
                };

                if (parent is Grid)
                {
                    Grid.SetColumn(shell, sliderColumn);
                    Grid.SetColumnSpan(shell, columnSpan);
                    Grid.SetRow(shell, Grid.GetRow(slider));
                    parent.Children.Add(shell);
                }
                else
                {
                    if (sliderIndex < 0) sliderIndex = parent.Children.Count;
                    parent.Children.Insert(sliderIndex, shell);
                }
                slider.Visibility = Visibility.Collapsed;
                numericSteppers[spec.SliderName] = shell;
            }
        }

        private RepeatButton BuildStepperButton(string glyph, string toolTip, Action adjust)
        {
            RepeatButton button = new RepeatButton();
            button.Content = glyph;
            button.Width = 34;
            button.Height = 30;
            button.FontSize = 16;
            button.FontWeight = FontWeights.SemiBold;
            button.Foreground = new SolidColorBrush(cFg2);
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.Delay = 420;
            button.Interval = 85;
            button.ToolTip = toolTip;
            AutomationProperties.SetName(button, toolTip);
            string hover = "#1F" + cFg.R.ToString("X2", CultureInfo.InvariantCulture)
                + cFg.G.ToString("X2", CultureInfo.InvariantCulture)
                + cFg.B.ToString("X2", CultureInfo.InvariantCulture);
            string templateXaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' "
                + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type RepeatButton}'>"
                + "<Border x:Name='stepperBg' Background='Transparent' CornerRadius='7'>"
                + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>"
                + "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'>"
                + "<Setter TargetName='stepperBg' Property='Background' Value='" + hover + "'/>"
                + "</Trigger></ControlTemplate.Triggers></ControlTemplate>";
            button.Template = (ControlTemplate)XamlReader.Parse(templateXaml);
            button.Click += delegate { adjust(); };
            return button;
        }

        private static void AdjustNumericSlider(Slider slider, double step, bool increase)
        {
            if (slider == null || step <= 0) return;
            if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0) step = slider.TickFrequency;
            double target = slider.Value + (increase ? step : -step);
            double rounded = slider.Minimum + Math.Round((target - slider.Minimum) / step) * step;
            slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, rounded));
        }

        private void SetNumericEnabled(string sliderName, bool enabled)
        {
            FrameworkElement host;
            if (numericSteppers.TryGetValue(sliderName, out host)) host.IsEnabled = enabled;
            Slider slider = ui.ContainsKey(sliderName) ? ui[sliderName] as Slider : null;
            if (slider != null) slider.IsEnabled = enabled;
        }

        private void RefreshControlAvailability()
        {
            if (root == null || !ui.ContainsKey("AutoActionsChk")) return;
            bool autoActions = ((CheckBox)ui["AutoActionsChk"]).IsChecked == true;
            bool walk = ((CheckBox)ui["WalkChk"]).IsChecked == true;
            bool snap = ((CheckBox)ui["SnapChk"]).IsChecked == true;
            bool edgeHide = ((CheckBox)ui["EdgeHideChk"]).IsChecked == true;
            bool cloneAuto = ((CheckBox)ui["CloneAutoChk"]).IsChecked == true;
            bool quips = ((CheckBox)ui["QuipsChk"]).IsChecked == true;
            bool weather = ((CheckBox)ui["WeatherChk"]).IsChecked == true;
            bool effect = !string.Equals(cfg.GetString("bubble", "effect", "off"),
                "off", StringComparison.OrdinalIgnoreCase);
            bool tail = ((CheckBox)ui["BubbleTailChk"]).IsChecked == true;

            SetNumericEnabled("ActionDurationSlider", autoActions);
            SetNumericEnabled("ActionPauseSlider", autoActions);
            ((Border)ui["ActionPoolCard"]).IsEnabled = autoActions;
            SetNumericEnabled("SpeedSlider", walk);
            ((CheckBox)ui["SnapAutoChk"]).IsEnabled = snap;
            SetNumericEnabled("SnapDistanceSlider", snap);
            ((TextBox)ui["EdgeDelayBox"]).IsEnabled = edgeHide;
            SetNumericEnabled("SliverSlider", edgeHide);
            ((CheckBox)ui["CloneCycleChk"]).IsEnabled = cloneAuto;
            SetNumericEnabled("ClonePauseSlider", cloneAuto);
            SetNumericEnabled("CloneJitterSlider", cloneAuto);
            ((Button)ui["QuipImportBtn"]).IsEnabled = quips;
            ((Button)ui["QuipExportBtn"]).IsEnabled = quips;
            ((TextBox)ui["WeatherCityBox"]).IsEnabled = weather;
            ((Button)ui["WeatherLocateBtn"]).IsEnabled = weather;
            SetNumericEnabled("BubbleSpeedSlider", effect);
            SetNumericEnabled("BubbleDepthSlider", effect);
            ((CheckBox)ui["BubbleReverseChk"]).IsEnabled = effect;
            SetNumericEnabled("BubbleTailSlider", tail);
        }

        private FrameworkElement BuildNavVisual(string title, bool selected)
        {
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            Border iconTile = new Border();
            iconTile.Width = 24;
            iconTile.Height = 24;
            iconTile.CornerRadius = new CornerRadius(7);
            Color accent = Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB"));
            iconTile.Background = new SolidColorBrush(selected
                ? Color.FromArgb(42, accent.R, accent.G, accent.B) : cChip);
            iconTile.VerticalAlignment = VerticalAlignment.Center;
            iconTile.Margin = new Thickness(0, 0, 8, 0);
            TextBlock glyph = OptionGlyph(NavIcon(title));
            glyph.FontSize = 11;
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Foreground = new SolidColorBrush(selected ? accent : cMuted);
            iconTile.Child = glyph;
            row.Children.Add(iconTile);
            TextBlock label = new TextBlock();
            label.Text = title;
            label.FontSize = 11.3;
            label.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            label.Foreground = new SolidColorBrush(selected ? cFg : cFg2);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(label);
            return row;
        }

        private static string NavIcon(string title)
        {
            switch (title)
            {
                case "主题与气泡": return "\uE790";
                case "宠物外观": return "\uE8B9";
                case "宠物设置": return "\uE713";
                case "余额": return "\uE9D2";
                case "套餐额度": return "\uE7B8";
                case "API 密钥": return "\uE8D7";
                case "应用更新": return "\uE895";
                default: return "\uE713";
            }
        }

        private void DecorateSettingsPage(StackPanel page, string title)
        {
            if (page == null) return;
            StackPanel header = new StackPanel();
            header.Margin = new Thickness(0, 2, 0, 10);
            TextBlock pageTitle = new TextBlock();
            pageTitle.Text = title;
            pageTitle.FontSize = 17;
            pageTitle.FontWeight = FontWeights.Bold;
            pageTitle.Foreground = new SolidColorBrush(cFg);
            header.Children.Add(pageTitle);
            TextBlock description = new TextBlock();
            description.Text = SettingsPageDescription(title);
            description.FontSize = 10;
            description.Foreground = new SolidColorBrush(cMuted);
            description.TextWrapping = TextWrapping.Wrap;
            description.Margin = new Thickness(0, 3, 0, 0);
            header.Children.Add(description);
            page.Children.Insert(0, header);

            for (int i = page.Children.Count - 1; i >= 0; i--)
            {
                TextBlock text = page.Children[i] as TextBlock;
                if (text == null || !IsSettingsGroupLabel(text.Text)) continue;
                page.Children.RemoveAt(i);
                page.Children.Insert(i, BuildSettingsGroupHeader(text.Text));
            }

            if (string.Equals(title, "主题与气泡", StringComparison.Ordinal))
            {
                InsertGroupHeader(page, "BubbleSpeedSlider", "动效参数");
                InsertGroupHeader(page, "BubbleFontSlider", "尺寸与字体");
                InsertGroupHeader(page, "BubbleRadiusSlider", "形状与质感");
            }
            else if (string.Equals(title, "宠物外观", StringComparison.Ordinal))
            {
                InsertGroupHeader(page, "RolePanel", "角色");
            }
            else if (string.Equals(title, "宠物设置", StringComparison.Ordinal))
            {
                InsertGroupHeader(page, "ScaleSlider", "显示与性能");
                InsertGroupHeader(page, "AutoActionsChk", "动作点播");
                InsertGroupHeader(page, "WalkChk", "行为");
                InsertGroupHeader(page, "QuipsChk", "台词与互动");
                InsertGroupHeader(page, "CloneAutoChk", "分身");
            }
            else if (string.Equals(title, "余额", StringComparison.Ordinal))
            {
                InsertGroupHeader(page, "ProviderList", "接口状态");
                InsertGroupHeader(page, "AlertChk", "余额提醒");
            }
            else if (string.Equals(title, "套餐额度", StringComparison.Ordinal))
            {
                InsertGroupHeader(page, "PlanList", "套餐状态");
                InsertGroupHeader(page, "QuotaChk", "额度提醒");
            }
            else if (string.Equals(title, "API 密钥", StringComparison.Ordinal))
            {
                InsertGroupHeader(page, "ProviderKeyList", "余额接口凭据");
                InsertGroupHeader(page, "PlanKeyList", "套餐接口凭据");
                InsertGroupHeader(page, "ProxyRow", "网络");
            }
        }

        private static string SettingsPageDescription(string title)
        {
            switch (title)
            {
                case "主题与气泡": return "界面主题、气泡外观、控件颜色与尺寸";
                case "宠物外观": return "角色素材与单图动态";
                case "宠物设置": return "显示性能、动作、行为、移动与分身";
                case "余额": return "余额接口状态、刷新与余额提醒";
                case "套餐额度": return "套餐窗口用量与额度提醒";
                case "API 密钥": return "接口密钥、平台 Token 与网络接入";
                case "应用更新": return "当前版本、公开 Release 与一键升级";
                default: return "";
            }
        }

        private static bool IsSettingsGroupLabel(string text)
        {
            return text == "界面" || text == "气泡外观" || text == "控件颜色"
                || text == "显示内容" || text == "素材工具";
        }

        private FrameworkElement BuildSettingsGroupHeader(string title)
        {
            Grid header = new Grid();
            header.Margin = new Thickness(0, 13, 0, 6);
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border rule = new Border();
            rule.Width = 3;
            rule.Height = 15;
            rule.CornerRadius = new CornerRadius(2);
            Color accent = Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB"));
            rule.Background = new SolidColorBrush(Color.FromArgb(185, accent.R, accent.G, accent.B));
            rule.Margin = new Thickness(0, 0, 7, 0);
            header.Children.Add(rule);
            TextBlock label = new TextBlock();
            label.Text = title;
            label.FontSize = 11.5;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = new SolidColorBrush(cFg2);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            header.Children.Add(label);
            return header;
        }

        private void InsertGroupHeader(StackPanel page, string anchorName, string title)
        {
            FrameworkElement anchor = ui.ContainsKey(anchorName)
                ? ui[anchorName] as FrameworkElement : null;
            if (anchor == null) return;
            UIElement topLevel = anchor;
            DependencyObject current = anchor;
            while (current != null)
            {
                DependencyObject parent = LogicalTreeHelper.GetParent(current);
                if (ReferenceEquals(parent, page)) break;
                UIElement element = parent as UIElement;
                if (element == null) break;
                topLevel = element;
                current = parent;
            }
            int index = page.Children.IndexOf(topLevel);
            if (index < 0) return;
            page.Children.Insert(index, BuildSettingsGroupHeader(title));
        }

        private void ShowPage(Button active, StackPanel page)
        {
            foreach (Button item in navButtons)
            {
                StackPanel target = item.Tag as StackPanel;
                if (target != null)
                {
                    target.Visibility = ReferenceEquals(target, page) ? Visibility.Visible : Visibility.Collapsed;
                }
                bool selected = ReferenceEquals(item, active);
                item.Content = BuildNavVisual(Convert.ToString(item.ToolTip,
                    CultureInfo.InvariantCulture), selected);
                item.Background = selected
                    ? new SolidColorBrush(Color.FromArgb(34,
                        Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")).R,
                        Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")).G,
                        Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")).B))
                    : Brushes.Transparent;
                item.BorderThickness = new Thickness(selected ? 1 : 0);
                item.BorderBrush = selected
                    ? new SolidColorBrush(Color.FromArgb(92,
                        Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")).R,
                        Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")).G,
                        Parse(cfg.GetStringAt("bubble.color1", "#4B6BFB")).B))
                    : Brushes.Transparent;
            }
            // 外观页的角色素材可能在外部被增删：切回该页时重新扫描 roles 目录。
            if (page != null && page.Children.Contains((UIElement)ui["RolePanel"]))
            {
                BuildRoleButtons();
                RefreshRigScope();
            }
            string pageTitle = Convert.ToString(active.ToolTip, CultureInfo.InvariantCulture);
            if (string.Equals(pageTitle, "宠物设置", StringComparison.Ordinal))
            {
                RefreshActionControls();
                ReorderActionPage(page);
            }
            UpdatePreviewDock(pageTitle);
            ScrollViewer scroller = ui.ContainsKey("PageScroll") ? ui["PageScroll"] as ScrollViewer : null;
            if (scroller != null)
            {
                scroller.ScrollToTop();
                scroller.Dispatcher.BeginInvoke(new Action(delegate { scroller.ScrollToTop(); }),
                    DispatcherPriority.Loaded);
            }
        }

        private void ReorderActionPage(StackPanel page)
        {
            if (page == null) return;
            FrameworkElement list = ui["ActionListCard"] as FrameworkElement;
            FrameworkElement pin = ui["ActionPinCard"] as FrameworkElement;
            FrameworkElement pool = ui["ActionPoolCard"] as FrameworkElement;
            FrameworkElement preview = ui["ActionPreviewCard"] as FrameworkElement;
            FrameworkElement auto = ui["AutoActionsChk"] as FrameworkElement;
            if (list == null || pin == null || pool == null || auto == null) return;

            page.Children.Remove(list);
            page.Children.Remove(pin);
            page.Children.Remove(pool);
            if (preview != null) page.Children.Remove(preview);

            int index = page.Children.IndexOf(auto);
            if (index < 0) index = 0;
            index++;
            page.Children.Insert(index++, list);
            page.Children.Insert(index++, pin);
            if (preview != null) page.Children.Insert(index++, preview);
            page.Children.Insert(index, pool);
        }

        private void UpdatePreviewDock(string pageTitle)
        {
            activePreviewPage = pageTitle ?? "";
            FrameworkElement source = null;
            string title = "实时预览";
            if (string.Equals(pageTitle, "主题与气泡", StringComparison.Ordinal))
            {
                source = ui["BubblePreviewCanvasHost"] as FrameworkElement;
                title = "气泡实时预览";
            }
            else if (string.Equals(pageTitle, "宠物设置", StringComparison.Ordinal))
            {
                source = ui["ActionPreviewCard"] as FrameworkElement;
                title = "宠物设置预览";
            }
            else if (string.Equals(pageTitle, "宠物外观", StringComparison.Ordinal))
            {
                FrameworkElement rig = ui["RigImageCard"] as FrameworkElement;
                if (rig != null && rig.Visibility == Visibility.Visible)
                {
                    source = rig;
                    title = "外观实时预览";
                }
                else
                {
                    title = "外观预览";
                }
            }
            else if (string.Equals(pageTitle, "余额", StringComparison.Ordinal))
                title = "余额状态预览";
            else if (string.Equals(pageTitle, "套餐额度", StringComparison.Ordinal))
                title = "套餐额度预览";
            else if (string.Equals(pageTitle, "API 密钥", StringComparison.Ordinal))
                title = "密钥接入预览";

            Border dock = ui["PreviewDock"] as Border;
            ContentControl visualHost = ui["PreviewVisualHost"] as ContentControl;
            ContentControl summaryHost = ui["PreviewSummaryHost"] as ContentControl;
            TextBlock caption = ui["PreviewDockTitle"] as TextBlock;
            if (dock == null || visualHost == null || summaryHost == null) return;
            if (string.Equals(pageTitle, "应用更新", StringComparison.Ordinal))
            {
                RestoreDockedPreview();
                visualHost.Content = null;
                visualHost.Visibility = Visibility.Collapsed;
                summaryHost.Content = null;
                summaryHost.Visibility = Visibility.Collapsed;
                dock.Visibility = Visibility.Collapsed;
                return;
            }
            if (source != null)
            {
                if (!ReferenceEquals(dockedPreview, source))
                {
                    RestoreDockedPreview();
                    Panel parent = source.Parent as Panel;
                    if (parent != null)
                    {
                        dockedPreviewParent = parent;
                        dockedPreviewIndex = parent.Children.IndexOf(source);
                        parent.Children.Remove(source);
                        source.Width = 340;
                        if (string.Equals(pageTitle, "主题与气泡", StringComparison.Ordinal))
                            source.Height = 150;
                        else if (string.Equals(pageTitle, "动作点播", StringComparison.Ordinal))
                            source.Height = 160;
                        dockedPreview = source;
                        dockedPreviewViewbox = new Viewbox();
                        dockedPreviewViewbox.Stretch = Stretch.Uniform;
                        dockedPreviewViewbox.StretchDirection = StretchDirection.Both;
                        dockedPreviewViewbox.Child = source;
                        visualHost.Content = dockedPreviewViewbox;
                    }
                }
                visualHost.Visibility = Visibility.Visible;
                summaryHost.Content = null;
                summaryHost.Visibility = Visibility.Collapsed;
                if (string.Equals(pageTitle, "主题与气泡", StringComparison.Ordinal))
                {
                    FrameworkElement fxHost = ui["FxHost"] as FrameworkElement;
                    Panel shellParent = fxHost == null ? null : fxHost.Parent as Panel;
                    if (shellParent != null)
                    {
                        fxHostShellParent = shellParent;
                        fxHostShellIndex = shellParent.Children.IndexOf(fxHost);
                        shellParent.Children.Remove(fxHost);
                    }
                }
            }
            else
            {
                RestoreDockedPreview();
                visualHost.Content = null;
                visualHost.Visibility = Visibility.Collapsed;
                summaryHost.Content = BuildPreviewSummary(pageTitle);
                summaryHost.Visibility = Visibility.Visible;
            }
            if (caption != null) caption.Text = title;
            dock.Visibility = Visibility.Visible;
        }

        private void RestoreDockedPreview()
        {
            if (dockedPreview == null) return;
            ContentControl visualHost = ui.ContainsKey("PreviewVisualHost")
                ? ui["PreviewVisualHost"] as ContentControl : null;
            if (visualHost != null) visualHost.Content = null;
            if (dockedPreviewViewbox != null)
            {
                dockedPreviewViewbox.Child = null;
                dockedPreviewViewbox = null;
            }
            if (dockedPreviewParent != null)
            {
                int index = Math.Max(0, Math.Min(
                    dockedPreviewIndex, dockedPreviewParent.Children.Count));
                dockedPreviewParent.Children.Insert(index, dockedPreview);
            }
            FrameworkElement previewCanvas = ui.ContainsKey("BubblePreviewCanvasHost")
                ? ui["BubblePreviewCanvasHost"] as FrameworkElement : null;
            if (ReferenceEquals(dockedPreview, previewCanvas))
            {
                FrameworkElement fxHost = ui.ContainsKey("FxHost")
                    ? ui["FxHost"] as FrameworkElement : null;
                if (fxHost != null && fxHostShellParent != null)
                {
                    int shellIndex = Math.Max(0, Math.Min(
                        fxHostShellIndex, fxHostShellParent.Children.Count));
                    fxHostShellParent.Children.Insert(shellIndex, fxHost);
                }
            }
            fxHostShellParent = null;
            fxHostShellIndex = -1;
            dockedPreview = null;
            dockedPreviewParent = null;
            dockedPreviewIndex = -1;
        }

        private void RefreshPreviewDock()
        {
            if (!string.IsNullOrEmpty(activePreviewPage))
                UpdatePreviewDock(activePreviewPage);
        }

        private FrameworkElement BuildPreviewSummary(string pageTitle)
        {
            StackPanel panel = new StackPanel();
            Snapshot data = engine.Data.Current;
            if (string.Equals(pageTitle, "余额", StringComparison.Ordinal))
            {
                ProviderState primary = data.Primary;
                int enabledProviders = 0;
                foreach (ProviderState provider in data.Providers)
                    if (provider.Enabled) enabledProviders++;
                AddPreviewLine(panel, "主接口",
                    primary == null ? "未连接"
                        : primary.Name + "  " + (primary.Ok
                            ? Fmt.Money(primary.Balance, primary.Currency) : "查询失败"),
                    primary != null && primary.Ok ? cOk : cMuted);
                AddPreviewLine(panel, "接口在线",
                    data.OkCount.ToString(CultureInfo.InvariantCulture) + " / "
                    + enabledProviders.ToString(CultureInfo.InvariantCulture),
                    data.OkCount > 0 ? cOk : cMuted);
                AddPreviewLine(panel, "余额提醒",
                    cfg.GetFlag("alert", "balanceWarn", true)
                    || cfg.GetFlag("alert", "budgetWarn", true) ? "已启用" : "已关闭",
                    cfg.GetFlag("alert", "balanceWarn", true)
                    || cfg.GetFlag("alert", "budgetWarn", true) ? cOk : cMuted);
            }
            else if (string.Equals(pageTitle, "API 密钥", StringComparison.Ordinal))
            {
                int providerKeys = 0;
                foreach (ProviderDef def in Providers.Effective(cfg))
                {
                    if (!def.HasBalance) continue;
                    if (cfg.GetStringAt("providers." + def.Id + ".apiKey", "") != "")
                        providerKeys++;
                }
                int planKeys = 0;
                foreach (PlanDef def in Plans.Catalog())
                {
                    if (cfg.GetStringAt("plans." + def.Id + ".apiKey", "") != "")
                        planKeys++;
                }
                AddPreviewLine(panel, "余额接口密钥",
                    providerKeys.ToString(CultureInfo.InvariantCulture) + " 个已配置", cFg2);
                AddPreviewLine(panel, "套餐接口密钥",
                    planKeys.ToString(CultureInfo.InvariantCulture) + " 个已配置", cFg2);
                AddPreviewLine(panel, "DeepSeek Token",
                    cfg.GetStringAt("providers.deepseek.platformToken", "") == "" ? "未配置" : "已配置",
                    cfg.GetStringAt("providers.deepseek.platformToken", "") == "" ? cMuted : cOk);
                AddPreviewLine(panel, "网络",
                    cfg.GetStringAt("network.proxy", "") == "" ? "直连" : "自定义代理",
                    cFg2);
            }
            else if (string.Equals(pageTitle, "套餐额度", StringComparison.Ordinal))
            {
                int shown = 0;
                foreach (PlanState plan in data.Plans)
                {
                    if (!plan.Enabled) continue;
                    AddPreviewLine(panel, plan.Name,
                        plan.Ok ? plan.Summary() : "未连接",
                        plan.Ok ? cFg2 : cMuted);
                    if (++shown >= 4) break;
                }
                if (shown == 0) AddPreviewLine(panel, "套餐", "未启用", cMuted);
                AddPreviewLine(panel, "额度提醒",
                    cfg.GetFlag("alert", "quotaWarn", true) ? "已启用" : "已关闭",
                    cfg.GetFlag("alert", "quotaWarn", true) ? cOk : cMuted);
            }
            else if (string.Equals(pageTitle, "分身", StringComparison.Ordinal))
            {
                AddPreviewLine(panel, "当前数量",
                    engine.Pets.Count.ToString(CultureInfo.InvariantCulture) + " 只", cFg2);
                AddPreviewLine(panel, "自动分身",
                    cfg.GetFlag("clones", "auto", true) ? "开启" : "关闭",
                    cfg.GetFlag("clones", "auto", true) ? cOk : cMuted);
                AddPreviewLine(panel, "数量上限",
                    cfg.GetNumber("clones", "max", 6).ToString("0", CultureInfo.InvariantCulture) + " 只",
                    cFg2);
                AddPreviewLine(panel, "角色模式",
                    cfg.GetString("clones", "roleMode", "random"), cFg2);
            }
            else if (string.Equals(pageTitle, "行为", StringComparison.Ordinal))
            {
                AddPreviewLine(panel, "自动动作",
                    cfg.GetFlag("actions", "enabled", true) ? "开启" : "关闭",
                    cfg.GetFlag("actions", "enabled", true) ? cOk : cMuted);
                AddPreviewLine(panel, "行走",
                    cfg.GetFlag("walk", "enabled", true) ? "开启" : "关闭",
                    cfg.GetFlag("walk", "enabled", true) ? cOk : cMuted);
                AddPreviewLine(panel, "自动贴边",
                    cfg.GetFlag("snap", "auto", false) ? "开启" : "关闭",
                    cfg.GetFlag("snap", "auto", false) ? cOk : cMuted);
                AddPreviewLine(panel, "作息",
                    cfg.GetFlag("sleep", "enabled", true) ? "开启" : "关闭",
                    cfg.GetFlag("sleep", "enabled", true) ? cOk : cMuted);
            }
            else
            {
                AddPreviewLine(panel, "当前外观",
                    cfg.GetString("", "role", "内置小鲸鱼"), cFg2);
                AddPreviewLine(panel, "大小",
                    cfg.GetNumber("", "scale", 1).ToString("0.00", CultureInfo.InvariantCulture) + "x",
                    cFg2);
                AddPreviewLine(panel, "角色随机",
                    cfg.GetFlag("roles", "random", false) ? "开启" : "关闭",
                    cfg.GetFlag("roles", "random", false) ? cOk : cMuted);
            }
            return panel;
        }

        private void AddPreviewLine(StackPanel panel, string label, string value, Color valueColor)
        {
            Grid row = new Grid();
            row.Margin = new Thickness(0, 0, 0, 7);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            TextBlock name = new TextBlock();
            name.Text = label;
            name.FontSize = 10.5;
            name.Foreground = new SolidColorBrush(cMuted);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(name);
            TextBlock amount = new TextBlock();
            amount.Text = value;
            amount.FontSize = 10.5;
            amount.FontWeight = FontWeights.SemiBold;
            amount.Foreground = new SolidColorBrush(valueColor);
            amount.TextAlignment = TextAlignment.Right;
            amount.TextTrimming = TextTrimming.CharacterEllipsis;
            amount.ToolTip = value;
            Grid.SetColumn(amount, 1);
            row.Children.Add(amount);
            panel.Children.Add(row);
        }

        private readonly List<Button> navButtons = new List<Button>();
        private readonly List<string> pageOrder = new List<string>();
        private string activePreviewPage = "";
        private FrameworkElement dockedPreview;
        private Viewbox dockedPreviewViewbox;
        private Panel dockedPreviewParent;
        private int dockedPreviewIndex;
        private Panel fxHostShellParent;
        private int fxHostShellIndex = -1;

        private static ControlTemplate SectionHeaderTemplate()
        {
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' "
                + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>"
                + "<Border x:Name='b' Background='#1B2236' CornerRadius='9'>"
                + "<ContentPresenter HorizontalAlignment='Left' VerticalAlignment='Center'/>"
                + "</Border>"
                + "<ControlTemplate.Triggers>"
                + "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#232C45'/></Trigger>"
                + "</ControlTemplate.Triggers>"
                + "</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }
    }
}
