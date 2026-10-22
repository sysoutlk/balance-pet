// Plays a PetPack: weighted state clips, looping idle chains, one-shot actions
// and a two-layer crossfade. Frames are loaded through the existing RoleAnim
// loader, so PNG folders, GIFs, single images and state folders all work.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BalancePet
{
    internal sealed class PackDirector
    {
        private const double DefaultDisplayHeight = 220;
        private readonly Config cfg;
        private readonly string roleId;
        private readonly PetPack pack;
        private readonly Grid host = new Grid();
        private readonly Image layerA = new Image();
        private readonly Image layerB = new Image();
        private PackClip current;
        private FrameSet currentSet;
        private PackClip next;
        private FrameSet nextSet;
        private double fade = 1;
        private double elapsed;
        private string state = "idle";
        private bool showingB;
        public double BaseWidth = Mascot.BaseWidth;
        public double BaseHeight = Mascot.BaseHeight;

        public PackDirector(Config config, string id, PetPack petPack)
        {
            cfg = config;
            roleId = id;
            pack = petPack;
            layerA.Stretch = Stretch.Fill;
            layerB.Stretch = Stretch.Fill;
            layerA.Opacity = 0;
            layerB.Opacity = 0;
            layerA.IsHitTestVisible = false;
            layerB.IsHitTestVisible = false;
            host.Children.Add(layerA);
            host.Children.Add(layerB);
            LoadInitial();
        }

        public FrameworkElement Visual { get { return host; } }
        public string CurrentClip { get { return current == null ? "" : current.Name; } }
        public string CurrentState { get { return state; } }

        private void LoadInitial()
        {
            List<string> idle = pack.StateClips("idle");
            if (idle == null || idle.Count == 0) return;
            PackClip clip = pack.Clip(idle[Program.Rng.Next(idle.Count)]);
            if (clip == null) return;
            current = clip;
            currentSet = LoadSet(clip);
            if (currentSet == null || currentSet.Empty) return;
            BitmapSource first = currentSet.Advance(0, 1);
            layerA.Source = first;
            layerA.Opacity = 1;
            SetBaseSize(currentSet.Frames[0]);
        }

        private void SetBaseSize(BitmapSource frame)
        {
            double width = Math.Max(1, frame.PixelWidth);
            double height = Math.Max(1, frame.PixelHeight);
            double scale = 1;
            if (pack.DisplayHeight > 0)
            {
                scale = pack.DisplayHeight / height;
            }
            else if (height > DefaultDisplayHeight)
            {
                scale = DefaultDisplayHeight / height;
            }
            if (pack.DisplayWidth > 0) scale = Math.Min(scale, pack.DisplayWidth / width);
            if (pack.DisplayUpscale
                && pack.DisplayWidth > 0
                && pack.DisplayHeight > 0)
            {
                scale = Math.Min(pack.DisplayWidth / width, pack.DisplayHeight / height);
            }
            BaseWidth = Math.Max(1, Math.Round(width * scale));
            BaseHeight = Math.Max(1, Math.Round(height * scale));
        }

        public void SetState(string wanted)
        {
            if (string.IsNullOrEmpty(wanted)) wanted = "idle";
            if (wanted == state && current != null && current.Loop) return;
            List<string> names = pack.StateClips(wanted);
            if (names == null || names.Count == 0)
            {
                names = pack.StateClips("idle");
                wanted = "idle";
            }
            if (names == null || names.Count == 0) return;
            state = wanted;
            string name = names[Program.Rng.Next(names.Count)];
            Log.Write("pack state " + state + " clip=" + name);
            StartClip(name);
        }

        /// <summary>Restarts the requested state from frame zero, even when it is already current.</summary>
        public void RestartState(string wanted)
        {
            if (string.IsNullOrEmpty(wanted)) wanted = "idle";
            List<string> names = pack.StateClips(wanted);
            if (names == null || names.Count == 0) return;
            string name = names[Program.Rng.Next(names.Count)];
            PackClip clip = pack.Clip(name);
            FrameSet set = clip == null ? null : LoadSet(clip);
            if (set == null || set.Empty) return;
            state = wanted;
            current = clip;
            currentSet = set;
            next = null;
            nextSet = null;
            fade = 1;
            elapsed = 0;
            showingB = false;
            layerA.Source = set.Advance(0, 1);
            layerA.Opacity = 1;
            layerA.Visibility = Visibility.Visible;
            layerB.Source = null;
            layerB.Opacity = 0;
            layerB.Visibility = Visibility.Collapsed;
            SetBaseSize(set.Frames[0]);
            Log.Write("pack state " + state + " clip=" + name + " restart");
        }

        private void StartClip(string name)
        {
            PackClip clip = pack.Clip(name);
            if (clip == null || (current != null && clip.Name == current.Name)) return;
            FrameSet set = LoadSet(clip);
            if (set == null || set.Empty) return;
            if (current == null)
            {
                current = clip;
                currentSet = set;
                layerA.Source = currentSet.Advance(0, 1);
                layerA.Opacity = 1;
                SetBaseSize(currentSet.Frames[0]);
                elapsed = 0;
                return;
            }
            next = clip;
            nextSet = set;
            Image incoming = showingB ? layerA : layerB;
            incoming.Source = nextSet.Advance(0, 1);
            incoming.Opacity = 0;
            incoming.Visibility = Visibility.Visible;
            fade = 0;
            elapsed = 0;
        }

        private FrameSet LoadSet(PackClip clip)
        {
            if (clip.Frames != null) return clip.Frames;
            string path = roleId + "/" + (string.IsNullOrEmpty(clip.Path) ? clip.Name : clip.Path);
            RoleAnim anim = RoleAnim.Load(cfg, path);
            if (anim == null) return null;
            FrameSet set = anim.For("idle");
            if (set == null || set.Empty) return null;
            set.Fps = clip.Fps;
            set.Reset();
            clip.Frames = set;
            if (clip.Width <= 0 || clip.Height <= 0)
            {
                clip.Width = set.Frames[0].PixelWidth;
                clip.Height = set.Frames[0].PixelHeight;
            }
            return set;
        }

        public void Update(double dt)
        {
            Update(dt, 1.0);
        }

        public void Update(double dt, double speed)
        {
            if (current == null || currentSet == null) return;
            elapsed += dt;
            if (speed < 0.1) speed = 0.1;
            BitmapSource frame = currentSet.Advance(dt, speed);
            Image outgoing = showingB ? layerB : layerA;
            if (frame != null) outgoing.Source = frame;

            if (fade < 1)
            {
                if (nextSet != null)
                {
                    BitmapSource incomingFrame = nextSet.Advance(dt, speed);
                    Image incoming = showingB ? layerA : layerB;
                    if (incomingFrame != null) incoming.Source = incomingFrame;
                }
                fade += dt / 0.22;
                if (fade > 1) fade = 1;
                outgoing.Opacity = 1 - fade;
                Image enter = showingB ? layerA : layerB;
                enter.Opacity = fade;
                if (fade >= 1)
                {
                    showingB = !showingB;
                    current = next;
                    currentSet = nextSet;
                    next = null;
                    nextSet = null;
                    elapsed = 0;
                    Image active = showingB ? layerB : layerA;
                    Image idle = showingB ? layerA : layerB;
                    active.Opacity = 1;
                    idle.Opacity = 0;
                    idle.Visibility = Visibility.Collapsed;
                }
                return;
            }

            if (current.Loop && elapsed >= current.Duration && pack.StateClips(state).Count > 1)
            {
                // Idle pool: after a loop, occasionally chain to another idle clip.
                if (Program.Rng.NextDouble() < 0.35)
                {
                    List<string> names = pack.StateClips(state);
                    StartClip(names[Program.Rng.Next(names.Count)]);
                }
                else
                {
                    elapsed = 0;
                }
            }
        }
    }
}
