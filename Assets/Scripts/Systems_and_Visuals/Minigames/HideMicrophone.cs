using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Микрофон для пряток: под кроватью (M-07) и в подвале (B-05). Первым в списке Windows часто стоит виртуальное
    // устройство (Steam Streaming и т.п.), которое пишет ровно ноль, поэтому настоящие пробуются раньше, а немое
    // устройство сменяется следующим. Рабочее запоминается на всю игру.
    public sealed class HideMicrophone
    {
        private const float DeadSeconds = 2f;       // ровно ноль столько времени — устройство немое или доступ запрещён
        private const float StallSeconds = 0.5f;    // позиция записи не двигается — микрофон отключили
        private static readonly string[] VirtualMarks =
            { "steam streaming", "cable", "vb-audio", "voicemeeter", "droidcam", "oculus", "virtual" };
        private static string workingDevice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => workingDevice = null;

        private readonly float[] window = new float[1024];
        private readonly List<string> candidates = new List<string>();
        private string device;
        private AudioClip clip;
        private int position;
        private float stalled, lastSound;

        public bool Available => clip != null;
        public float Level { get; private set; } = VoiceNoiseGate.SilenceDb;
        public string Device => device;
        // Устройство сменилось: фон комнаты надо мерить заново.
        public event Action DeviceChanged;

        public static bool IsVirtual(string name)
        {
            string lower = name.ToLowerInvariant();
            foreach (string mark in VirtualMarks) if (lower.Contains(mark)) return true;
            return false;
        }

        // Сначала то, что уже работало, потом настоящие устройства, виртуальные — в конце.
        public static List<string> Order(string[] devices, string working)
        {
            var result = new List<string>();
            if (devices == null) return result;
            foreach (string name in devices) if (name == working) result.Add(name);
            foreach (string name in devices) if (name != working && !IsVirtual(name)) result.Add(name);
            foreach (string name in devices) if (name != working && IsVirtual(name)) result.Add(name);
            return result;
        }

        public void Start()
        {
            Stop();
            candidates.Clear();
            // Выбранное в настройках — первым; если оно немое или отключено, дальше идут остальные, как раньше.
            string chosen = SettingsStore.GetMicrophone();
            bool chosenPresent = !string.IsNullOrEmpty(chosen) && Array.IndexOf(Microphone.devices, chosen) >= 0;
            candidates.AddRange(Order(Microphone.devices, chosenPresent ? chosen : workingDevice));
            Next();
        }

        public void Stop()
        {
            if (clip != null && Microphone.IsRecording(device)) Microphone.End(device);
            clip = null;
            Level = VoiceNoiseGate.SilenceDb;
        }

        private void Next()
        {
            Stop();
            if (candidates.Count == 0) { DeviceChanged?.Invoke(); return; }
            device = candidates[0];
            candidates.RemoveAt(0);
            clip = Microphone.Start(device, true, 1, 16000);
            position = 0;
            stalled = 0f;
            lastSound = Time.time;
            DeviceChanged?.Invoke();
        }

        // Последние ~64 мс с микрофона (буфер не пересоздаётся каждый кадр).
        public void Sample()
        {
            if (clip == null) return;
            if (!Microphone.IsRecording(device)) { Next(); return; }
            int current = Microphone.GetPosition(device);
            if (current == position)
            {
                stalled += Time.unscaledDeltaTime;
                if (stalled > StallSeconds) Next();
                return;
            }
            position = current;
            stalled = 0f;
            int offset = current - window.Length;
            if (offset < 0) offset += clip.samples;
            if (!clip.GetData(window, offset)) return;
            Level = VoiceNoiseGate.ToDb(window, window.Length);
            if (Level > VoiceNoiseGate.SilenceDb + 0.5f)
            {
                lastSound = Time.time;
                workingDevice = device;
            }
            else if (Time.time - lastSound > DeadSeconds) Next();
        }
    }
}
