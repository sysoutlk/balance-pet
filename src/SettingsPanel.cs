// Modern settings card (dark, rounded, custom switches/sliders) built from an inline
// XAML template with single-quoted attributes so the source stays readable in C# 5.
// Provider rows and key inputs are generated in code because the provider list is data.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        ShowInTaskbar='False' SizeToContent='WidthAndHeight' ResizeMode='NoResize'
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
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
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
      <Setter Property='Height' Value='24'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Slider'>
            <Grid Background='Transparent'>
              <Border Height='4' CornerRadius='2' Background='__TRACK__' VerticalAlignment='Center'/>
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
      <Setter Property='Foreground' Value='__MUTED__'/>
      <Setter Property='Margin' Value='1,16,0,7'/>
    </Style>
    <Style x:Key='Val' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='Margin' Value='8,0,0,0'/>
    </Style>
    <Style x:Key='Lbl' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='__FG2__'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
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
      <ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' MaxHeight='640'>
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
      <StackPanel x:Name='ContentRoot' Width='510'>
        <DockPanel x:Name='Header' Background='Transparent'>
          <Button x:Name='QuitBtn' DockPanel.Dock='Right' Content='退出桌宠' Width='76' Height='26' Margin='0' FontSize='11' Style='{StaticResource Btn}' Foreground='#E5484D' BorderBrush='#E5484D' BorderThickness='1' ToolTip='退出桌宠（结束进程）'/>
          <StackPanel Margin='0,2,0,0'>
            <TextBlock x:Name='TitleText' Text='余额桌宠' FontSize='15' FontWeight='Bold' Foreground='__FG__'/>
            <TextBlock x:Name='SubText' Text='…' FontSize='10' Foreground='__MUTED__' Margin='0,3,0,0'/>
          </StackPanel>
        </DockPanel>
        <DockPanel Margin='0,6,0,0'>
          <Border x:Name='NavHost' DockPanel.Dock='Left' Width='140' Background='__NAV__' CornerRadius='10' Padding='6,6,7,6' Margin='0,0,8,0'>
            <StackPanel x:Name='Nav'/>
          </Border>
          <StackPanel x:Name='Main'/>
        </DockPanel>
        <TextBlock Style='{StaticResource Sect}' Text='主题与气泡'/>
        <WrapPanel Margin='0,2,0,0'>
          <Button x:Name='ThemeDropBtn' Content='主题 · 浅色 ▾' Width='126' Style='{StaticResource Btn}'/>
          <Button x:Name='EffectDropBtn' Content='动效 · 关闭 ▾' Width='126' Style='{StaticResource Btn}'/>
          <Button x:Name='FontDropBtn' Content='字体 · 系统 ▾' Width='132' Style='{StaticResource Btn}'/>
          <Button x:Name='NumberFontDropBtn' Content='数字 · 跟随 ▾' Width='146' Style='{StaticResource Btn}'/>
          <CheckBox x:Name='BubbleTrendChk' Style='{StaticResource Switch}' Content='图表'/>
          <CheckBox x:Name='BubbleTailChk' Style='{StaticResource Switch}' Content='小尾巴'/>
          <CheckBox x:Name='BubbleReverseChk' Style='{StaticResource Switch}' Content='反向'/>
        </WrapPanel>
        <Border x:Name='FxHost' CornerRadius='12' Padding='12' Margin='0,8,0,0' Background='__ROW__' BorderBrush='__BORDER__' BorderThickness='1'>
          <StackPanel>
            <DockPanel>
              <TextBlock Text='实时预览' FontSize='10' Foreground='__MUTED__' DockPanel.Dock='Left'/>
              <TextBlock x:Name='FxHint' FontSize='10' Foreground='__MUTED__' HorizontalAlignment='Right'/>
            </DockPanel>
            <Border Margin='0,8,0,0' Height='150' CornerRadius='8' Background='__DESK__'>
            <Grid>
              <Canvas x:Name='FxCanvas' Height='150' ClipToBounds='False'>
                <Border x:Name='FxCard' CornerRadius='12' Background='__CARD__' BorderBrush='__BORDER__' BorderThickness='1' Padding='10,8'>
                  <StackPanel>
                    <StackPanel Orientation='Horizontal'>
                      <Ellipse x:Name='FxDot' Width='7' Height='7' VerticalAlignment='Center' Margin='0,0,6,0'/>
                      <TextBlock x:Name='FxTitle' FontSize='11' FontWeight='SemiBold' Text='余额'/>
                    </StackPanel>
                    <TextBlock x:Name='FxAmount' FontSize='19' FontWeight='Bold' Text='¥ 000.00' Margin='0,3,0,0'/>
                    <TextBlock x:Name='FxRow' FontSize='10' Text='今日已用 ¥ 00.00' Margin='0,2,0,0'/>
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
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='字号' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleFontVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleFontSlider' Width='156' Minimum='0.7' Maximum='1.8' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='宽度' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='BubbleWidthVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='BubbleWidthSlider' Width='156' Minimum='150' Maximum='320' Style='{StaticResource Flat}'/>
          </StackPanel>
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
        <StackPanel Orientation='Horizontal' Margin='1,2,0,0'>
          <TextBlock Text='主题色' FontSize='11' Foreground='__MUTED__' VerticalAlignment='Center' Margin='0,0,6,0'/>
          <WrapPanel x:Name='Color1Panel'/>
          <Button x:Name='PickThemeBtn' Content='调色盘…' Width='76' Style='{StaticResource Btn}'/>
        </StackPanel>
        <StackPanel Orientation='Horizontal' Margin='1,4,0,0'>
          <TextBlock Text='混合配色' FontSize='11' Foreground='__MUTED__' VerticalAlignment='Center' Margin='0,0,6,0'/>
          <WrapPanel x:Name='MixPanel'/>
          <Button x:Name='MixAddBtn' Content='添加颜色…' Width='88' Style='{StaticResource Btn}'/>
        </StackPanel>
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
        <UniformGrid x:Name='ActionGrid' Columns='4'/>
        <CheckBox x:Name='AutoActionsChk' Style='{StaticResource Switch}' Content='闲时自动动作' Margin='0,7,0,0'/>
        <DockPanel Margin='0,6,0,2'>
          <Button x:Name='ActionPoolAllBtn' Content='全选' Width='46' Style='{StaticResource Mini}'/>
          <Button x:Name='ActionPoolNoneBtn' Content='清空' Width='46' Style='{StaticResource Mini}'/>
        </DockPanel>
        <WrapPanel x:Name='ActionPoolPanel'/>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='单次时长'/>
          <Slider x:Name='ActionDurationSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='0.5' Maximum='3'/>
          <TextBlock x:Name='ActionDurationVal' Grid.Column='2' Style='{StaticResource Val}' Text='1.0x'/>
        </Grid>
        <Grid Margin='0,4,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='动作间隔'/>
          <Slider x:Name='ActionPauseSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='6' Maximum='90'/>
          <TextBlock x:Name='ActionPauseVal' Grid.Column='2' Style='{StaticResource Val}' Text='14s'/>
        </Grid>
        <TextBlock Style='{StaticResource Sect}' Text='外观'/>
        <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,0,0,4'>
          <Button x:Name='RoleEditBtn' Content='编辑' Width='56' Style='{StaticResource Mini}' ToolTip='管理模式：添加 / 删除角色素材'/>
        </StackPanel>
        <WrapPanel x:Name='RolePanel'/>
        <DockPanel Margin='0,2,0,0'>
          <Button x:Name='RoleAddBtn' Content='添加素材…' Width='92' Style='{StaticResource Btn}' Visibility='Collapsed'/>
          <Button x:Name='AnimMakeBtn' Content='图片转动图…' Width='110' Style='{StaticResource Btn}'/>
          <TextBlock x:Name='AnimMakeStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='6,0,0,0'
                     TextTrimming='CharacterEllipsis' ToolTip='选一张 PNG，自动生成含全部动作的逐帧角色（存在 roles 目录，可再逐帧精修）'/>
        </DockPanel>
        <DockPanel Margin='0,2,0,0'>
          <Button x:Name='HatPickBtn' Content='戴帽子…' Width='92' Style='{StaticResource Btn}'/>
          <Button x:Name='HatClearBtn' Content='摘掉' Width='62' Style='{StaticResource Btn}'/>
          <TextBlock x:Name='HatStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='6,0,0,0'
                     TextTrimming='CharacterEllipsis' ToolTip='挑一张透明底 PNG 当帽子（会复制到 props 目录），下面调大小和位置'/>
        </DockPanel>
        <WrapPanel Margin='0,2,0,0'>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='帽子大小' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='HatSizeVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='HatSizeSlider' Width='156' Minimum='0.1' Maximum='1.4' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='帽子左右' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='HatXVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='HatXSlider' Width='156' Minimum='0' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
          <StackPanel Width='156' Margin='0,0,10,6'>
            <DockPanel><TextBlock Text='帽子上下' FontSize='10' Foreground='__MUTED__'/><TextBlock x:Name='HatYVal' FontSize='10' Foreground='__FGLABEL__' HorizontalAlignment='Right'/></DockPanel>
            <Slider x:Name='HatYSlider' Width='156' Minimum='-0.2' Maximum='1' Style='{StaticResource Flat}'/>
          </StackPanel>
        </WrapPanel>
        <CheckBox x:Name='RandomRoleChk' Style='{StaticResource Switch}' Content='随机外观（每次启动与分裂随机挑一个角色）' Margin='0,7,0,0' FontSize='11'/>
        <TextBlock Style='{StaticResource Sect}' Text='行为'/>
        <WrapPanel>
          <CheckBox x:Name='WalkChk' Style='{StaticResource Switch}' Content='散步'/>
          <CheckBox x:Name='SnapChk' Style='{StaticResource Switch}' Content='贴边'/>
          <CheckBox x:Name='SleepChk' Style='{StaticResource Switch}' Content='作息'/>
          <CheckBox x:Name='QuipsChk' Style='{StaticResource Switch}' Content='台词'/>
          <CheckBox x:Name='CollideChk' Style='{StaticResource Switch}' Content='碰撞带动'/>
        </WrapPanel>
        <DockPanel Margin='0,2,0,0'>
          <Button x:Name='QuipImportBtn' Content='导入台词…' Width='92' Style='{StaticResource Btn}'/>
          <Button x:Name='QuipExportBtn' Content='导出台词…' Width='92' Style='{StaticResource Btn}'/>
          <TextBlock x:Name='QuipStatus' Style='{StaticResource Lbl}' FontSize='10' Margin='6,0,0,0'
                     TextTrimming='CharacterEllipsis' ToolTip='JSON：{ 「打招呼」: [「…」], 「蹦一下」: [「…」] }；也认英文动作名，导入后自动去重合并'/>
        </DockPanel>
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
        <Grid Margin='0,6,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='56'/><ColumnDefinition Width='*'/><ColumnDefinition Width='46'/>
          </Grid.ColumnDefinitions>
          <TextBlock Style='{StaticResource Lbl}' Text='速度'/>
          <Slider x:Name='SpeedSlider' Grid.Column='1' Style='{StaticResource Flat}' Minimum='25' Maximum='170'/>
          <TextBlock x:Name='SpeedVal' Grid.Column='2' Style='{StaticResource Val}' Text='72'/>
        </Grid>
        <TextBlock Style='{StaticResource Sect}' Text='余额接口'/>
        <StackPanel x:Name='ProviderList'/>
        
        <WrapPanel x:Name='UnsupportedPanel'/>
        <DockPanel Margin='0,6,0,0'>
          <Button x:Name='RefreshBtn' DockPanel.Dock='Right' Content='刷新余额' Width='88' Style='{StaticResource Btn}' Margin='0'/>
          <TextBlock x:Name='DataText' Style='{StaticResource Lbl}' Text='…' TextTrimming='CharacterEllipsis'/>
        </DockPanel>
        <DockPanel Margin='0,6,0,0'>
          <TextBlock Style='{StaticResource Lbl}' Text='HTTPS 代理' Width='66'/>
          <TextBox x:Name='ProxyBox' Style='{StaticResource TxtBox}'/>
        </DockPanel>
        <TextBlock FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='留空 = 直连；填 http://127.0.0.1:7890 之类的地址可让查询走指定代理（网络出口被限制 HTTP 451 时很有用）。'/>
        <TextBlock Style='{StaticResource Sect}' Text='套餐额度'/>
        <StackPanel x:Name='PlanList'/>
        <TextBlock Style='{StaticResource Sect}' Text='提醒'/>
        <WrapPanel>
          <CheckBox x:Name='AlertChk' Style='{StaticResource Switch}' Content='余额预警'/>
          <CheckBox x:Name='BudgetChk' Style='{StaticResource Switch}' Content='预算提醒'/>
          <CheckBox x:Name='QuotaChk' Style='{StaticResource Switch}' Content='套餐额度提醒'/>
        </WrapPanel>
        <StackPanel x:Name='AlertList' Margin='0,4,0,0'/>
        <TextBlock FontSize='10' Foreground='__MUTED__' TextWrapping='Wrap' Margin='1,3,0,0'
                   Text='按平台分别设：余额低于这个数就预警、今日消耗到这个数就提醒；留空 = 该平台不提醒。'/>
        <TextBlock Style='{StaticResource Sect}' Text='分身'/>
        <DockPanel Margin='0,0,0,6'>
          <CheckBox x:Name='CloneAutoChk' Style='{StaticResource Switch}' Content='自动分裂' Margin='0,0,10,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='上限'/>
          <TextBox x:Name='CloneMaxBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='只（0 = 不限）' Margin='5,0,0,0'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,6'>
          <CheckBox x:Name='CloneCycleChk' Style='{StaticResource Switch}' Content='循环涨落' Margin='0,0,10,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='下限'/>
          <TextBox x:Name='CloneMinBox' Style='{StaticResource TxtBox}' Width='52' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='只（涨到上限后慢慢收回，再涨回去）' Margin='5,0,0,0' FontSize='10'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,6'>
          <TextBlock Style='{StaticResource Lbl}' Text='分裂间隔'/>
          <TextBox x:Name='ClonePauseBox' Style='{StaticResource TxtBox}' Width='56' Margin='8,0,0,0'/>
          <TextBlock Style='{StaticResource Lbl}' Text='秒起（实际为 1~2.5 倍之间的随机）' Margin='5,0,0,0' FontSize='10'/>
        </DockPanel>
        <DockPanel>
          <StackPanel DockPanel.Dock='Right' Orientation='Horizontal'>
            <Button x:Name='CloneAddBtn' Content='来一个' Width='72' Style='{StaticResource Btn}'/>
            <Button x:Name='CloneRemoveBtn' Content='收一个' Width='72' Margin='0' Style='{StaticResource Btn}'/>
          </StackPanel>
          <TextBlock x:Name='CloneCount' Style='{StaticResource Lbl}' Text='1 只'/>
        </DockPanel>

      </StackPanel>
    </ScrollViewer>
    </Border>
  </Grid>
</Window>";

        private readonly Config cfg;
        private readonly Engine engine;
        private Window root;
        private Grid panelShadowLayer;       // 面板阴影层（挖掉卡片形状，只露轮廓外一圈）
        private Border panelShadowShape;
        private Border panelCard;
        private const double PanelShadowPad = 40;
        private readonly Dictionary<string, FrameworkElement> ui = new Dictionary<string, FrameworkElement>();
        private readonly Dictionary<string, TextBlock> statusById = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, PasswordBox> keyBoxes = new Dictionary<string, PasswordBox>();
        private readonly DispatcherTimer previewTimer = new DispatcherTimer();
        private double previewPhase;
        private bool updating;
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

        public SettingsPanel(Config config, Engine engineRef)
        {
            cfg = config;
            engine = engineRef;
            Pinned = false;
        }

        public void Build()
        {
            // 主题色（bubble.color1）同时给设置卡片的高亮着色：一处设置，两处生效。
            string accent = cfg.GetStringAt("bubble.color1", "#4B6BFB");
            if (string.IsNullOrEmpty(accent) || !accent.StartsWith("#", StringComparison.Ordinal)) accent = "#4B6BFB";
            root = (Window)XamlReader.Parse(PanelXaml(accent));
            panelShadowLayer = (Grid)root.FindName("PanelShadowLayer");
            panelShadowShape = (Border)root.FindName("PanelShadowShape");
            panelCard = (Border)root.FindName("PanelCard");
            if (panelCard != null) panelCard.SizeChanged += delegate { UpdatePanelShadowClip(); };
            foreach (string name in new string[]
            {
                "Header","TitleText","SubText","ActionGrid","AutoActionsChk","RolePanel","ScaleSlider","ScaleVal",
                "OpacitySlider","OpacityVal","WalkChk","SnapChk","SnapDistanceSlider","SnapDistanceVal","SleepChk","QuipsChk","CollideChk","SpeedSlider","SpeedVal",
                "QuipImportBtn","QuipExportBtn","QuipStatus",
                "FpsSlider","FpsVal","IdleFpsSlider","IdleFpsVal",
                "ActionPauseSlider","ActionPauseVal","ActionPoolPanel","ActionPoolAllBtn","ActionPoolNoneBtn","ActionDurationSlider","ActionDurationVal","ProviderList","UnsupportedPanel","RefreshBtn","DataText",
                "PlanList",
                "RandomRoleChk","RoleEditBtn","RoleAddBtn","AnimMakeBtn","AnimMakeStatus","ProxyBox","EdgeHideChk","EdgeDelayBox","AlertList","AlertChk","BudgetChk","QuotaChk",
                "HatPickBtn","HatClearBtn","HatStatus","HatSizeSlider","HatSizeVal","HatXSlider","HatXVal","HatYSlider","HatYVal",
                "SliverSlider","SliverVal",
                "ContentRoot","Main","Nav",
                "ThemeDropBtn","EffectDropBtn","FontDropBtn","NumberFontDropBtn","BubbleTrendChk","BubbleTailChk","BubbleSpeedSlider","BubbleSpeedVal",
                "BubbleReverseChk","BubbleDepthSlider","BubbleDepthVal","BubbleFontSlider","BubbleFontVal",
                "BubbleWidthSlider","BubbleWidthVal","BubbleRadiusSlider","BubbleRadiusVal","BubbleTailSlider",
                "BubbleOpacitySlider","BubbleOpacityVal","BubbleShadowSlider","Color1Panel",
                "MixPanel",
                "FxHost","FxHint","FxCanvas","FxCard","FxDot","FxTitle","FxAmount","FxRow","FxTail","BubbleShadowVal",
                "PickThemeBtn","MixAddBtn",
                "CloneAutoChk","CloneMaxBox","CloneAddBtn","CloneRemoveBtn","CloneCount","QuitBtn"
                ,"ClonePauseBox"
                ,"CloneCycleChk","CloneMinBox"
            })
            {
                ui[name] = (FrameworkElement)root.FindName(name);
            }
            root.Deactivated += delegate { if (!Pinned) HidePanel(); };
            root.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) HidePanel(); };
            ((FrameworkElement)ui["Header"]).MouseLeftButtonDown += delegate { try { root.DragMove(); } catch { } };


            Button randomButton = new Button();
            randomButton.Content = "随机";
            randomButton.Style = (Style)root.Resources["Accent"];
            // 动作点播只演示动作，不弹台词气泡（气泡留给自动动作、提醒和点击查看）。
            randomButton.Click += delegate { engine.PerformAction(ActivePet, "random", false); };
            ((UniformGrid)ui["ActionGrid"]).Children.Add(randomButton);
            foreach (string key in PetWindow.ActionKeys)
            {
                Button button = new Button();
                button.Content = Engine.ActionName(key);
                button.Style = (Style)root.Resources["Btn"];
                string action = key;
                button.Click += delegate { engine.PerformAction(ActivePet, action, false); };
                ((UniformGrid)ui["ActionGrid"]).Children.Add(button);
            }
            BuildActionPoolChips();
            ((Button)ui["ActionPoolAllBtn"]).Click += delegate
            {
                cfg.SetAt("actions.pool", PetWindow.ActionKeys);
                cfg.Save();
                BuildActionPoolChips();
            };
            ((Button)ui["ActionPoolNoneBtn"]).Click += delegate
            {
                cfg.SetAt("actions.pool", new string[0]);
                cfg.Save();
                BuildActionPoolChips();
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
                int min = (int)e.NewValue;
                cfg.Set("actions", "pauseMinSec", min);
                cfg.Set("actions", "pauseMaxSec", (int)(min * 2.6));
                ((TextBlock)ui["ActionPauseVal"]).Text = min.ToString(CultureInfo.InvariantCulture) + "s";
                cfg.Save();
            };
            BindSwitch("AutoActionsChk", "actions", "enabled");
            BindSwitch("WalkChk", "walk", "enabled");
            BindSwitch("SnapChk", "snap", "enabled");
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
            BindSwitch("EdgeHideChk", "edgeHide", "enabled");
            BindSwitch("CloneAutoChk", "clones", "auto");
            BindSwitch("RandomRoleChk", "roles", "random");
            BindSwitch("BubbleTrendChk", "bubble", "trend");
            BindSwitch("BubbleTailChk", "bubble", "tail");
            WireDropdown("ThemeDropBtn", ThemeValues, ThemeLabels, "bubble.theme", true);
            WireDropdown("EffectDropBtn", EffectValues, EffectLabels, "bubble.effect", true);
            WireDropdown("FontDropBtn", FontValues, FontLabels, "bubble.font", false);
            WireDropdown("NumberFontDropBtn", NumberFontValues, NumberFontLabels, "bubble.numberFont", false);
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
            BindBubbleSlider("BubbleWidthSlider", "bubble.width", "BubbleWidthVal", "0");
            BindBubbleSlider("BubbleRadiusSlider", "bubble.radius", "BubbleRadiusVal", "0");
            BindBubbleSlider("BubbleTailSlider", "bubble.tailSize", "BubbleTailSliderVal", "0");
            BindBubbleSlider("BubbleOpacitySlider", "bubble.opacity", "BubbleOpacityVal", "0%");
            BindBubbleSlider("BubbleShadowSlider", "bubble.shadow", "BubbleShadowSliderVal", "0.00");
            ((Button)ui["MixAddBtn"]).ToolTip = "从调色盘取色；超过上限会自动顶掉最早设置的颜色";
            ((Button)ui["QuipImportBtn"]).Click += delegate { ImportQuipsDialog(); };
            ((Button)ui["QuipExportBtn"]).Click += delegate { ExportQuipsDialog(); };
            ((Button)ui["RoleAddBtn"]).Click += delegate { AddRoleAssetDialog(); };
            ((Button)ui["AnimMakeBtn"]).Click += delegate { MakeAnimDialog(); };
            ((Button)ui["HatPickBtn"]).Click += delegate { HatPickDialog(); };
            ((Button)ui["HatClearBtn"]).Click += delegate
            {
                cfg.Set("props", "hat", "");
                cfg.Save();
                RefreshProps();
                SetHatStatus("已摘掉");
            };
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
            ((Slider)ui["HatSizeSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("props.hatSize", Math.Round(e.NewValue, 2));
                ((TextBlock)ui["HatSizeVal"]).Text = ((int)Math.Round(e.NewValue * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                cfg.Save();
                RefreshProps();
            };
            ((Slider)ui["HatXSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("props.hatX", Math.Round(e.NewValue, 2));
                ((TextBlock)ui["HatXVal"]).Text = ((int)Math.Round(e.NewValue * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                cfg.Save();
                RefreshProps();
            };
            ((Slider)ui["HatYSlider"]).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (updating) return;
                cfg.SetAt("props.hatY", Math.Round(e.NewValue, 2));
                ((TextBlock)ui["HatYVal"]).Text = ((int)Math.Round(e.NewValue * 100)).ToString(CultureInfo.InvariantCulture) + "%";
                cfg.Save();
                RefreshProps();
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
            BindNumberBox("ClonePauseBox", "clones.pauseMinSec");
            BindSwitch("CloneCycleChk", "clones", "cycle");
            BindNumberBox("CloneMinBox", "clones.min");
            BindField("ProxyBox", "network.proxy");
            ((Button)ui["RefreshBtn"]).Click += delegate { engine.Data.Refresh(); };
            ((Button)ui["CloneAddBtn"]).Click += delegate { engine.AddClone(); Update(); };
            ((Button)ui["CloneRemoveBtn"]).Click += delegate { engine.RemoveClone(); Update(); };
            ((Button)ui["QuitBtn"]).Click += delegate
            {
                if (ConfirmDialog("退出桌宠", "退出后桌宠与余额提醒都会停止运行。", "退出")) engine.Quit();
            };
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(root, "添加失败：" + ex.Message, "添加角色素材", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetAnimStatus(string text)
        {
            TextBlock status = ui.ContainsKey("AnimMakeStatus") ? ui["AnimMakeStatus"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
        }

        /// <summary>选一张图当帽子：复制到 props\ 目录再挂上（透明底 PNG 效果最好）。</summary>
        private void HatPickDialog()
        {
            System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = "选一张帽子（透明底 PNG 最好）";
            dialog.Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            string name = System.IO.Path.GetFileName(dialog.FileName);
            try
            {
                string target = System.IO.Path.Combine(AppPaths.PropsDir, name);
                if (!string.Equals(dialog.FileName, target, StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Copy(dialog.FileName, target, true);
                }
            }
            catch
            {
            }
            cfg.Set("props", "hat", name);
            cfg.Save();
            RefreshProps();
            SetHatStatus("已戴上 " + name + "（下面调大小和位置）");
        }

        private void RefreshProps()
        {
            foreach (PetWindow pet in engine.Pets) pet.RefreshProp();
        }

        private void SetHatStatus(string text)
        {
            TextBlock status = ui.ContainsKey("HatStatus") ? ui["HatStatus"] as TextBlock : null;
            if (status == null) return;
            status.Text = text;
            status.ToolTip = text;
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

        private void BindField(string name, string path)
        {
            TextBox box = (TextBox)ui[name];
            box.LostFocus += delegate { SaveField(path, box.Text); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) SaveField(path, box.Text);
            };
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
                engine.Data.Refresh();
                UpdateLabel(labelName, e.NewValue, format);
            };
        }

        private void UpdateLabel(string labelName, double value, string format)
        {
            if (string.IsNullOrEmpty(labelName)) return;
            TextBlock label = ui.ContainsKey(labelName) ? ui[labelName] as TextBlock : null;
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
            if (path == "clones.pauseMinSec")
            {
                // 分裂间隔：填的是最小值，实际在 1~2.5 倍之间随机。
                cfg.SetAt("clones.pauseMaxSec", Math.Round(value * 2.5));
            }
            cfg.Save();
            engine.Data.Refresh();
            Update();
        }

        // ------------------------------------------------------------ 角色
        /// <summary>动作点播页的「闲时自动播放池」：点动作名切换是否参与自动播放。</summary>
        private void BuildActionPoolChips()
        {
            WrapPanel panel = (WrapPanel)ui["ActionPoolPanel"];
            panel.Children.Clear();
            List<string> pool = cfg.GetStrings("actions", "pool");
            foreach (string key in PetWindow.ActionKeys)
            {
                string action = key;
                bool on = pool.Contains(key);
                Button chip = new Button();
                chip.Style = (Style)root.Resources["Mini"];
                chip.Content = Engine.ActionName(key);
                chip.FontSize = 10.5;
                chip.Height = 26;
                chip.Padding = new Thickness(9, 0, 9, 0);
                chip.Margin = new Thickness(0, 0, 5, 5);
                chip.Opacity = on ? 1.0 : 0.5;
                chip.BorderThickness = new Thickness(on ? 2 : 0);
                chip.BorderBrush = on ? Brush("#4B6BFB") : Brushes.Transparent;
                chip.ToolTip = on ? "闲时自动播放中（点击移出）" : "未参与自动播放（点击加入）";
                chip.Click += delegate
                {
                    List<string> current = cfg.GetStrings("actions", "pool");
                    if (current.Contains(action)) current.Remove(action);
                    else current.Add(action);
                    cfg.SetAt("actions.pool", current.ToArray());
                    cfg.Save();
                    BuildActionPoolChips();
                };
                panel.Children.Add(chip);
            }
        }

        private void BuildRoleButtons()
        {
            WrapPanel panel = (WrapPanel)ui["RolePanel"];
            panel.Children.Clear();   // 刷新前先清空，避免重复叠加
            string selected = cfg.GetString("", "role", "");
            AddRoleButton(panel, "", "内置小鲸鱼", selected, null);
            List<string> files = AppPaths.RoleFiles();
            foreach (string file in files)
            {
                AddRoleButton(panel, file, System.IO.Path.GetFileNameWithoutExtension(file), selected, file);
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
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(text);
            button.Content = stack;
            string chosen = tag;
            button.Click += delegate
            {
                cfg.Set("", "role", chosen);
                cfg.SetAt("roles.random", false);   // an explicit pick wins over random looks
                foreach (PetWindow pet in engine.Pets) pet.ApplyRole(chosen);
                cfg.Save();
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
            Button add = ui.ContainsKey("RoleAddBtn") ? ui["RoleAddBtn"] as Button : null;
            if (add != null) add.Visibility = roleEditMode ? Visibility.Visible : Visibility.Collapsed;
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
            WrapPanel unsupported = (WrapPanel)ui["UnsupportedPanel"];
            list.Children.Clear();
            unsupported.Children.Clear();
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
            }
        }

        private FrameworkElement BuildProviderRow(ProviderDef def)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Background = new SolidColorBrush(cRow);
            row.Padding = new Thickness(10, 7, 10, 9);
            row.Margin = new Thickness(0, 0, 0, 7);
            StackPanel stack = new StackPanel();

            DockPanel head = new DockPanel();
            TextBlock status = new TextBlock();
            status.FontSize = 10.5;
            status.Foreground = new SolidColorBrush(cMuted);
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
                authHint.Foreground = new SolidColorBrush(cMuted);
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
                pair.Value.ToolTip = state != null && !string.IsNullOrEmpty(state.ErrorDetail) ? state.ErrorDetail : null;
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
            StackPanel list = (StackPanel)ui["AlertList"];
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
            if (signature == alertRowKey && list.Children.Count > 0) return;
            alertRowKey = signature;
            list.Children.Clear();
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
                list.Children.Add(AlertGroup("余额与预算（按平台）"));
                foreach (UIElement row in providerRows) list.Children.Add(row);
            }
            if (planRows.Count > 0)
            {
                list.Children.Add(AlertGroup("套餐额度（按套餐）"));
                foreach (UIElement row in planRows) list.Children.Add(row);
            }
            if (providerRows.Count == 0 && planRows.Count == 0)
            {
                TextBlock hint = new TextBlock();
                hint.Text = "先在「余额接口」或「套餐额度」里启用平台，它们的阈值就会出现在这里。";
                hint.FontSize = 10;
                hint.Foreground = new SolidColorBrush(cMuted);
                hint.Margin = new Thickness(1, 2, 0, 2);
                list.Children.Add(hint);
            }
        }

        /// <summary>提醒页的小分组标题：开多少显示多少，分组只是让页面好读。</summary>
        private TextBlock AlertGroup(string text)
        {
            TextBlock group = new TextBlock();
            group.Text = text;
            group.FontSize = 10;
            group.Foreground = new SolidColorBrush(cMuted);
            group.Margin = new Thickness(1, 4, 0, 4);
            return group;
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

        private void BuildPlanRows()
        {
            StackPanel list = (StackPanel)ui["PlanList"];
            list.Children.Clear();
            foreach (PlanDef def in Plans.Catalog())
            {
                Border row = new Border();
                row.CornerRadius = new CornerRadius(9);
                row.Background = new SolidColorBrush(cRow);
                row.Padding = new Thickness(10, 7, 10, 9);
                row.Margin = new Thickness(0, 0, 0, 7);
                StackPanel stack = new StackPanel();

                DockPanel head = new DockPanel();
                TextBlock status = new TextBlock();
                status.FontSize = 10.5;
                status.Foreground = new SolidColorBrush(cMuted);
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
                pair.Value.ToolTip = state != null && !string.IsNullOrEmpty(state.ErrorDetail) ? state.ErrorDetail : null;
            }
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

        public void ShowFor(PetWindow pet)
        {
            ActivePet = pet;
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
                SyncBubbleSlider("ActionDurationSlider", "ActionDurationVal", cfg.GetNumberAt("actions.durationScale", 1), "0.0x");
                ((CheckBox)ui["AutoActionsChk"]).IsChecked = cfg.GetFlag("actions", "enabled", true);
                ((CheckBox)ui["WalkChk"]).IsChecked = cfg.GetFlag("walk", "enabled", true);
                ((CheckBox)ui["SnapChk"]).IsChecked = cfg.GetFlag("snap", "enabled", true);
                SyncBubbleSlider("SnapDistanceSlider", "SnapDistanceVal", cfg.GetNumberAt("snap.distancePx", 56), "0 px");
                ((CheckBox)ui["SleepChk"]).IsChecked = cfg.GetFlag("sleep", "enabled", true);
                ((CheckBox)ui["QuipsChk"]).IsChecked = cfg.GetFlag("alert", "quipsOn", true);
                ((CheckBox)ui["CollideChk"]).IsChecked = cfg.GetFlagAt("pets.collide", true);
                ((CheckBox)ui["AlertChk"]).IsChecked = cfg.GetFlag("alert", "balanceWarn", true);
                ((CheckBox)ui["BudgetChk"]).IsChecked = cfg.GetFlag("alert", "budgetWarn", true);
                ((CheckBox)ui["QuotaChk"]).IsChecked = cfg.GetFlag("alert", "quotaWarn", true);
                ((CheckBox)ui["EdgeHideChk"]).IsChecked = cfg.GetFlag("edgeHide", "enabled", true);
                ((CheckBox)ui["CloneAutoChk"]).IsChecked = cfg.GetFlagAt("clones.auto", true);
                ((CheckBox)ui["RandomRoleChk"]).IsChecked = cfg.GetFlagAt("roles.random", true);
                string fontNow = cfg.GetString("bubble", "font", "");
                ((Button)ui["ThemeDropBtn"]).Content = "主题：" + DropLabel(ThemeValues, ThemeLabels, "bubble.theme", "浅色") + "  ▾";
                ((Button)ui["EffectDropBtn"]).Content = "特效：" + DropLabel(EffectValues, EffectLabels, "bubble.effect", "关闭") + "  ▾";
                ((Button)ui["FontDropBtn"]).Content = "字体：" + DropLabel(FontValues, FontLabels, "bubble.font", "跟随系统") + "  ▾";
                ((Button)ui["NumberFontDropBtn"]).Content = "数字：" + DropLabel(NumberFontValues, NumberFontLabels, "bubble.numberFont", "跟随字体") + "  ▾";
                // 设置卡片本身也跟随同一个字体，保证整卡观感一致。
                root.FontFamily = string.IsNullOrEmpty(fontNow)
                    ? System.Windows.SystemFonts.MessageFontFamily
                    : new FontFamily(fontNow);
                ((CheckBox)ui["BubbleTrendChk"]).IsChecked = cfg.GetFlag("bubble", "trend", true);
                ((CheckBox)ui["BubbleTailChk"]).IsChecked = cfg.GetFlag("bubble", "tail", true);
                double bubbleSpeed = cfg.GetNumber("bubble", "speed", 1.0);
                ((Slider)ui["BubbleSpeedSlider"]).Value = bubbleSpeed;
                ((TextBlock)ui["BubbleSpeedVal"]).Text = bubbleSpeed.ToString("0.0", CultureInfo.InvariantCulture) + "x";
                ((CheckBox)ui["BubbleReverseChk"]).IsChecked = cfg.GetFlag("bubble", "effectReverse", false);
                SyncBubbleSlider("BubbleDepthSlider", "BubbleDepthVal", cfg.GetNumberAt("bubble.effectDepth", 1), "0.0");
                SyncBubbleSlider("BubbleFontSlider", "BubbleFontVal", cfg.GetNumberAt("bubble.fontScale", 1), "0.0x");
                SyncBubbleSlider("BubbleWidthSlider", "BubbleWidthVal", cfg.GetNumberAt("bubble.width", 196), "0");
                SyncBubbleSlider("BubbleRadiusSlider", "BubbleRadiusVal", cfg.GetNumberAt("bubble.radius", 16), "0");
                SyncBubbleSlider("BubbleTailSlider", null, cfg.GetNumberAt("bubble.tailSize", 13), "0");
                SyncBubbleSlider("BubbleOpacitySlider", "BubbleOpacityVal", cfg.GetNumberAt("bubble.opacity", 1), "0%");
                SyncBubbleSlider("BubbleShadowSlider", null, cfg.GetNumberAt("bubble.shadow", 0.28), "0.00");
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
                SyncBubbleSlider("HatSizeSlider", "HatSizeVal", cfg.GetNumberAt("props.hatSize", 0.55), "0%");
                SyncBubbleSlider("HatXSlider", "HatXVal", cfg.GetNumberAt("props.hatX", 0.5), "0%");
                SyncBubbleSlider("HatYSlider", "HatYVal", cfg.GetNumberAt("props.hatY", 0.04), "0%");
                SyncBubbleSlider("SliverSlider", "SliverVal", cfg.GetNumberAt("edgeHide.sliverRatio", 0.32), "0%");
                MarkPalette("Color1Panel", cfg.GetStringAt("bubble.color1", "#4B6BFB"));
                MarkMixPalette();
                UpdatePreview();
                BuildAlertRows();
                ((TextBox)ui["EdgeDelayBox"]).Text = cfg.GetNumberAt("edgeHide.delaySec", 10).ToString("0.#", CultureInfo.InvariantCulture);
                ((TextBox)ui["CloneMaxBox"]).Text = ((int)engine.CloneCap()).ToString(CultureInfo.InvariantCulture);
                ((TextBox)ui["ClonePauseBox"]).Text = ((int)cfg.GetNumberAt("clones.pauseMinSec", 60)).ToString(CultureInfo.InvariantCulture);
                ((CheckBox)ui["CloneCycleChk"]).IsChecked = cfg.GetFlagAt("clones.cycle", true);
                ((TextBox)ui["CloneMinBox"]).Text = ((int)engine.CloneFloor()).ToString(CultureInfo.InvariantCulture);
                ((TextBox)ui["ProxyBox"]).Text = cfg.GetStringAt("network.proxy", "");
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
            // 面板标题可自定义（ui.title）；默认用通用名，不绑定具体平台。
            string title = cfg.GetStringAt("ui.title", "余额桌宠");
            if (string.IsNullOrEmpty(title)) title = "余额桌宠";
            ((TextBlock)ui["TitleText"]).Text = title;
            root.Title = title;
            Snapshot data = engine.Data.Current;
            ProviderState primary = data.Primary;
            string peakText = engine.PeakText();
            ((TextBlock)ui["DataText"]).Text = primary == null
                ? "无接口"
                : primary.Name + " · " + Fmt.Money(primary.Balance, primary.Currency)
                    + (peakText.Length > 0 ? " · " + peakText : "");
            string peakLine = engine.PeakLine();
            ((TextBlock)ui["SubText"]).Text = (peakLine.Length > 0 ? peakLine + "　" : "")
                + data.OkCount + " 个接口在线";
            UpdateProviderStatus();
            UpdatePlanStatus();
        }

        // ------------------------------------------------------------ 气泡强调色 / 动态配色
        // 预置色不再铺在主界面：统一收进调色盘底部，主界面只展示"当前选中"。
        private static readonly string[] ColorPresets = new string[]
        {
            "#4B6BFB", "#7C5CFF", "#A855F7", "#E8517B", "#F08A24", "#FBBF24", "#12B5A5", "#10B981", "#38BDF8", "#F472B6",
            "#F87171", "#0EA5E9"
        };

        // 主题 / 特效 / 字体：下拉选项（简单、好懂，一处设置）
        private static readonly string[] ThemeValues = new string[] { "light", "dark", "system", "glass", "frost", "monet" };
        private static readonly string[] ThemeLabels = new string[] { "浅色", "深色", "跟随系统", "液体玻璃", "磨砂", "莫奈取色" };
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
                card.Child = list;
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
                        if (refresh) engine.Data.Refresh();
                        if (path == "bubble.theme")
                        {
                            // 主题同时作用于设置卡片本身：重建一次面板让配色立即生效。
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

        /// <summary>主题色：只显示当前颜色（对勾 + 光晕），点色块或「调色盘…」都能换色。</summary>
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
            themeSwatchBox.ToolTip = "当前主题色 " + hex.ToUpperInvariant() + "，点一下打开调色盘";

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

        /// <summary>主题色改色：气泡主色 + 设置卡片强调色一起换，副色按主题色自动配一个。</summary>
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
                engine.Data.Refresh();
                RebuildForAccent(true);
            });
        }

        /// <summary>主题色会注入卡片模板，改色后重建面板让高亮色立即生效。</summary>
        /// <summary>
        /// 面板的配色与气泡共用同一套主题：深色系/浅色系/莫奈都会替换掉 XAML 里的占位符，
        /// 主题色则替换强调色，做到「一处设置，气泡与设置卡片同时跟随」。
        /// </summary>
        private string PanelXaml(string accent)
        {
            string theme = cfg.GetString("bubble", "theme", "");
            if (string.IsNullOrEmpty(theme)) theme = cfg.GetString("bubble", "style", "light");
            ThemePalette palette = BubbleTheme.Resolve(theme);
            bool dark = palette.Dark;
            string card = dark ? "#F2171C2B" : "#F7FFFFFF";
            string cardBorder = dark ? "#2EFFFFFF" : "#1E2C3A6E";
            if (theme == "glass") card = "#E0FFFFFF";
            if (theme == "frost") card = "#F0F5FFFF";
            if (theme == "monet") card = ToHex(palette.Card);
            string row = dark ? "#1B2236" : "#F1F4FA";
            string field = dark ? "#121828" : "#FFFFFF";
            string track = dark ? "#2A3350" : "#E3E8F3";
            string button = dark ? "#252D48" : "#EDF0F8";
            string buttonHover = dark ? "#33406A" : "#E1E6F4";
            string border = dark ? "#39456B" : "#D9DFEC";
            string chip = dark ? "#202A42" : "#E9EDF7";
            string nav = dark ? "#171D2B" : "#F1F4FA";
            string desk = dark ? "#0F1421" : "#E7EBF5";
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
            if (theme == "glass") nav = "#B3FFFFFF";
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
            if (themeSwatchBox != null) themeSwatchBox.ToolTip = "当前主题色 " + current.ToUpperInvariant() + "，点一下打开调色盘";
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
            ThemePalette palette = BubbleTheme.Resolve(theme);
            Border card = (Border)ui["FxCard"];
            Border tail = (Border)ui["FxTail"];
            card.Background = new SolidColorBrush(palette.Card);
            card.BorderBrush = new SolidColorBrush(palette.Border);
            ((TextBlock)ui["FxTitle"]).Foreground = new SolidColorBrush(palette.Title);
            ((TextBlock)ui["FxRow"]).Foreground = new SolidColorBrush(palette.Muted);
            ((TextBlock)ui["FxAmount"]).FontSize = 19 * cfg.GetNumber("bubble", "fontScale", 1.0);
            double radius = cfg.GetNumber("bubble", "radius", 16);
            // 预览忠实反映真实圆角（不再打 0.7 折），阴影也跟着圆角走，与气泡一致。
            card.CornerRadius = new CornerRadius(Math.Max(2, Math.Min(40, radius)));
            double width = cfg.GetNumber("bubble", "width", 196);
            card.Width = 110 + (Math.Max(150, Math.Min(320, width)) - 150) / 170.0 * 180;
            card.Opacity = cfg.GetNumber("bubble", "opacity", 1.0);
            // 预览要真的"看得出"：宽度映射更宽、圆角/尾巴/阴影/字号/透明度都放大呈现。
            card.UpdateLayout();
            double tailSize = Math.Max(6, Math.Min(26, cfg.GetNumber("bubble", "tailSize", 13)));
            previewShadowW = -1;   // 尺寸/圆角标记失效，下一帧重算阴影层

            tail.Width = tailSize;
            tail.Height = tailSize;
            tail.Background = card.Background;
            tail.BorderBrush = card.BorderBrush;
            ((TextBlock)ui["FxHint"]).Text = DropLabel(EffectValues, EffectLabels, "bubble.effect", "关闭")
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
            ((TextBlock)ui["FxTitle"]).Foreground = brush;
            ((TextBlock)ui["FxAmount"]).Foreground = brush;
            ((Border)ui["FxCard"]).BorderBrush = new SolidColorBrush(Color.FromArgb(150, list[0].R, list[0].G, list[0].B));
            // 尾巴贴在卡片底部中间。
            Border card = (Border)ui["FxCard"];
            Border tail = (Border)ui["FxTail"];
            Canvas.SetLeft(card, 6);
            Canvas.SetTop(card, 6);
            double cardWidth = card.Width;
            double cardHeight = card.ActualHeight > 0 ? card.ActualHeight : 66;
            Canvas.SetLeft(tail, 6 + cardWidth / 2 - tail.Width / 2);
            Canvas.SetTop(tail, 6 + cardHeight - tail.Height / 2 - 2);
            bool showTail = cfg.GetFlag("bubble", "tail", true);
            tail.Visibility = showTail ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreviewShadow(6, 6, cardWidth, cardHeight);
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
            StackPanel content = (StackPanel)ui["ContentRoot"];
            StackPanel nav = (StackPanel)ui["Nav"];
            StackPanel host = (StackPanel)ui["Main"];
            List<UIElement> children = new List<UIElement>();
            foreach (UIElement child in content.Children) children.Add(child);
            content.Children.Clear();
            for (int i = 0; i < children.Count && i < 2; i++) content.Children.Add(children[i]);

            Style sectionStyle = (Style)root.Resources["Sect"];
            navButtons.Clear();
            pageOrder.Clear();
            StackPanel page = null;
            for (int i = 2; i < children.Count; i++)
            {
                UIElement child = children[i];
                TextBlock text = child as TextBlock;
                if (text != null && ReferenceEquals(text.Style, sectionStyle))
                {
                    string title = text.Text;
                    page = new StackPanel();
                    page.Visibility = Visibility.Collapsed;
                    host.Children.Add(page);
                    Button item = new Button();
                    item.Style = (Style)root.Resources["Btn"];
                    item.Width = 120;
                    item.Height = 28;
                    item.Margin = new Thickness(0, 0, 0, 4);
                    item.Padding = new Thickness(8, 0, 8, 0);
                    item.FontSize = 11;
                    item.HorizontalContentAlignment = HorizontalAlignment.Left;
                    item.Content = title;
                    item.ToolTip = title;   // 名字太长时悬停看全称
                    item.Tag = page;
                    item.BorderThickness = new Thickness(0);
                    item.BorderBrush = Brushes.Transparent;
                    Button button = item;
                    item.Click += delegate { ShowPage(button, (StackPanel)button.Tag); };
                    nav.Children.Add(item);
                    navButtons.Add(item);
                    pageOrder.Add(title);
                    continue;
                }
                if (page != null) page.Children.Add(child);
            }
            if (navButtons.Count > 0) ShowPage(navButtons[0], (StackPanel)navButtons[0].Tag);
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
                item.BorderThickness = new Thickness(ReferenceEquals(item, active) ? 2 : 0);
                item.BorderBrush = ReferenceEquals(item, active) ? Brush("#4B6BFB") : Brushes.Transparent;
            }
            // 外观页的角色素材可能在外部被增删：切回该页时重新扫描 roles 目录。
            if (page != null && page.Children.Contains((UIElement)ui["RolePanel"])) BuildRoleButtons();
        }

        private readonly List<Button> navButtons = new List<Button>();
        private readonly List<string> pageOrder = new List<string>();

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
