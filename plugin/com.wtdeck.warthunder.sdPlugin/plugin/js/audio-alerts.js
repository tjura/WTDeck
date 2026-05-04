(function () {
  class AudioAlerts {
    constructor() {
      this.audioContext = null;
    }

    playAlert(pattern, options) {
      if (pattern === "gWarning") {
        return this.playGWarning(options || {});
      }
      if (pattern === "gDanger") {
        return this.playGDanger(options || {});
      }
      if (pattern === "terrainWarning") {
        return this.playTerrainWarning(options || {});
      }
      if (pattern === "terrainDanger") {
        return this.playTerrainDanger(options || {});
      }
      if (pattern === "pullUp") {
        return this.playPullUp(options || {});
      }
      if (pattern === "lowFuelVoice") {
        return this.playLowFuelVoice(options || {});
      }
      return this.playTone(options || {});
    }

    playGWarning(options) {
      return this.playScheduled((audioContext, startedAt) => {
        scheduleTone(audioContext, {
          frequency: numberOrDefault(options.frequency, 420),
          durationMs: numberOrDefault(options.durationMs, 90),
          volume: numberOrDefault(options.volume, 0.12),
          type: options.type || "triangle",
          startedAt: startedAt
        });
      });
    }

    playGDanger(options) {
      return this.playScheduled((audioContext, startedAt) => {
        const toneMs = numberOrDefault(options.toneMs, 150);
        const lowToneMs = numberOrDefault(options.lowToneMs, 180);
        const gapMs = numberOrDefault(options.gapMs, 55);
        const volume = numberOrDefault(options.volume, 0.28);

        scheduleTone(audioContext, {
          frequency: numberOrDefault(options.highFrequency, 880),
          durationMs: toneMs,
          volume: volume,
          type: options.type || "triangle",
          startedAt: startedAt
        });
        scheduleTone(audioContext, {
          frequency: numberOrDefault(options.lowFrequency, 660),
          durationMs: lowToneMs,
          volume: volume * 0.85,
          type: options.type || "triangle",
          startedAt: startedAt + (toneMs + gapMs) / 1000
        });
      });
    }

    playDanger(options) {
      return this.playAlert("gDanger", options || {});
    }

    playLowFuelVoice(options) {
      if (options.src && this.playAudioFile(options)) {
        return true;
      }
      if (this.playSpeech(options)) {
        return true;
      }
      return this.playTerrainWarning({
        lowFrequency: numberOrDefault(options.lowFrequency, 520),
        highFrequency: numberOrDefault(options.highFrequency, 760),
        toneMs: numberOrDefault(options.toneMs, 180),
        gapMs: numberOrDefault(options.gapMs, 80),
        volume: numberOrDefault(options.volume, 0.28)
      });
    }

    playAudioFile(options) {
      if (!window.Audio) {
        return false;
      }
      try {
        const audio = new Audio(options.src);
        audio.volume = clamp(numberOrDefault(options.volume, 0.85), 0.0001, 1);
        const playResult = audio.play();
        if (playResult && playResult.catch) {
          playResult.catch(() => {});
        }
        return true;
      } catch (_error) {
        return false;
      }
    }

    playSpeech(options) {
      if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) {
        return false;
      }
      try {
        const utterance = new SpeechSynthesisUtterance(options.text || "LOW FUEL");
        utterance.rate = numberOrDefault(options.rate, 0.92);
        utterance.pitch = numberOrDefault(options.pitch, 0.72);
        utterance.volume = clamp(numberOrDefault(options.volume, 0.9), 0.0001, 1);
        const voices = window.speechSynthesis.getVoices
          ? window.speechSynthesis.getVoices()
          : [];
        utterance.voice = chooseMaleVoice(voices);
        window.speechSynthesis.speak(utterance);
        return true;
      } catch (_error) {
        return false;
      }
    }

    playTerrainWarning(options) {
      return this.playScheduled((audioContext, startedAt) => {
        const toneMs = numberOrDefault(options.toneMs, 170);
        const gapMs = numberOrDefault(options.gapMs, 95);
        const volume = numberOrDefault(options.volume, 0.22);
        scheduleToneSequence(audioContext, startedAt, [
          {
            frequency: numberOrDefault(options.lowFrequency, 420),
            durationMs: toneMs,
            volume: volume,
            type: options.type || "sawtooth",
            offsetMs: 0
          },
          {
            frequency: numberOrDefault(options.highFrequency, 620),
            durationMs: toneMs,
            volume: volume * 0.86,
            type: options.type || "sawtooth",
            offsetMs: toneMs + gapMs
          }
        ]);
      });
    }

    playTerrainDanger(options) {
      return this.playScheduled((audioContext, startedAt) => {
        const toneMs = numberOrDefault(options.toneMs, 140);
        const gapMs = numberOrDefault(options.gapMs, 45);
        const volume = numberOrDefault(options.volume, 0.30);
        scheduleToneSequence(audioContext, startedAt, [
          {
            frequency: numberOrDefault(options.lowFrequency, 520),
            durationMs: toneMs,
            volume: volume,
            type: options.type || "square",
            offsetMs: 0
          },
          {
            frequency: numberOrDefault(options.highFrequency, 880),
            durationMs: toneMs,
            volume: volume * 0.9,
            type: options.type || "square",
            offsetMs: toneMs + gapMs
          },
          {
            frequency: numberOrDefault(options.lowFrequency, 520),
            durationMs: toneMs,
            volume: volume,
            type: options.type || "square",
            offsetMs: (toneMs + gapMs) * 2
          }
        ]);
      });
    }

    playPullUp(options) {
      return this.playScheduled((audioContext, startedAt) => {
        const toneMs = numberOrDefault(options.toneMs, 120);
        const gapMs = numberOrDefault(options.gapMs, 35);
        const volume = numberOrDefault(options.volume, 0.34);
        scheduleToneSequence(audioContext, startedAt, [
          {
            frequency: numberOrDefault(options.highFrequency, 1040),
            durationMs: toneMs,
            volume: volume,
            type: options.type || "square",
            offsetMs: 0
          },
          {
            frequency: numberOrDefault(options.lowFrequency, 660),
            durationMs: toneMs,
            volume: volume * 0.92,
            type: options.type || "square",
            offsetMs: toneMs + gapMs
          },
          {
            frequency: numberOrDefault(options.highFrequency, 1040),
            durationMs: toneMs,
            volume: volume,
            type: options.type || "square",
            offsetMs: (toneMs + gapMs) * 2
          }
        ]);
      });
    }

    playTone(options) {
      return this.playScheduled((audioContext, startedAt) => {
        scheduleTone(audioContext, {
          frequency: numberOrDefault(options.frequency, 880),
          durationMs: numberOrDefault(options.durationMs, 180),
          volume: numberOrDefault(options.volume, 0.3),
          type: options.type || "sine",
          startedAt: startedAt
        });
      });
    }

    playScheduled(schedule) {
      const AudioContextConstructor = window.AudioContext || window.webkitAudioContext;
      if (!AudioContextConstructor) {
        return false;
      }

      try {
        if (!this.audioContext) {
          this.audioContext = new AudioContextConstructor();
        }
        if (this.audioContext.state === "suspended" && this.audioContext.resume) {
          this.audioContext.resume().catch(() => {});
        }

        schedule(this.audioContext, this.audioContext.currentTime);
        return true;
      } catch (_error) {
        return false;
      }
    }
  }

  function scheduleToneSequence(audioContext, startedAt, tones) {
    tones.forEach((tone) => {
      scheduleTone(audioContext, {
        frequency: tone.frequency,
        durationMs: tone.durationMs,
        volume: tone.volume,
        type: tone.type,
        startedAt: startedAt + numberOrDefault(tone.offsetMs, 0) / 1000
      });
    });
  }

  function scheduleTone(audioContext, options) {
    const startedAt = options.startedAt || audioContext.currentTime;
    const durationSeconds = Math.max(0.01, numberOrDefault(options.durationMs, 180) / 1000);
    const attackSeconds = Math.min(0.012, durationSeconds / 3);
    const releaseSeconds = Math.min(0.045, durationSeconds / 2);
    const volume = clamp(numberOrDefault(options.volume, 0.3), 0.0001, 1);
    const oscillator = audioContext.createOscillator();
    const gain = audioContext.createGain();

    oscillator.type = options.type || "sine";
    oscillator.frequency.setValueAtTime(numberOrDefault(options.frequency, 880), startedAt);
    gain.gain.setValueAtTime(0.0001, startedAt);
    gain.gain.exponentialRampToValueAtTime(volume, startedAt + attackSeconds);
    gain.gain.setValueAtTime(volume, startedAt + Math.max(attackSeconds, durationSeconds - releaseSeconds));
    gain.gain.exponentialRampToValueAtTime(0.0001, startedAt + durationSeconds);

    oscillator.connect(gain);
    gain.connect(audioContext.destination);
    oscillator.start(startedAt);
    oscillator.stop(startedAt + durationSeconds + 0.03);
    oscillator.onended = function () {
      oscillator.disconnect();
      gain.disconnect();
    };
  }

  function chooseMaleVoice(voices) {
    if (!Array.isArray(voices) || voices.length === 0) {
      return null;
    }
    const preferred = voices.find((voice) =>
      /david|mark|male|guy|george/i.test(voice.name || "")
    );
    if (preferred) {
      return preferred;
    }
    const english = voices.find((voice) => /^en/i.test(voice.lang || ""));
    return english || voices[0] || null;
  }

  function numberOrDefault(value, fallback) {
    return Number.isFinite(value) ? value : fallback;
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  window.WTDeckAudioAlerts = AudioAlerts;
})();
