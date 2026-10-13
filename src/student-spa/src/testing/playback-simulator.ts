import { vi } from 'vitest';

// The media boundary supplies browser events that jsdom cannot produce itself.
export const simulateVideo = () => {
  const paused = new WeakMap<HTMLMediaElement, boolean>();
  vi.spyOn(HTMLMediaElement.prototype, 'paused', 'get').mockImplementation(function (this: HTMLMediaElement) {
    return paused.get(this) ?? true;
  });
  const play = vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(function (this: HTMLMediaElement) {
    paused.set(this, false); this.dispatchEvent(new Event('play')); return Promise.resolve();
  });
  const pause = vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(function (this: HTMLMediaElement) {
    paused.set(this, true); this.dispatchEvent(new Event('pause'));
  });
  return { play, pause };
};

// jsdom has no text track loader. This boundary reads the actual in-memory WebVTT
// Blob and supplies the same activeCues/cuechange events as a browser.
export const simulateCaptions = () => {
  const blobs = new Map<string, Blob>();
  const tracks = new Map<HTMLTrackElement, SimulatedTextTrack>();
  let nextUrl = 0;
  const createObjectURL = vi.fn((blob: Blob) => {
    const url = `blob:captions-${++nextUrl}`; blobs.set(url, blob); return url;
  });
  const revokeObjectURL = vi.fn((url: string) => { blobs.delete(url); });
  vi.stubGlobal('URL', class extends URL {
    static createObjectURL = createObjectURL;
    static revokeObjectURL = revokeObjectURL;
  });
  const descriptor = Object.getOwnPropertyDescriptor(HTMLTrackElement.prototype, 'track');
  Object.defineProperty(HTMLTrackElement.prototype, 'track', { configurable: true, get(this: HTMLTrackElement) {
    let track = tracks.get(this);
    if (!track) { track = new SimulatedTextTrack(); tracks.set(this, track); }
    return track;
  } });
  const listeners = new Map<HTMLVideoElement, () => void>();
  const observer = new MutationObserver((changes) => {
    for (const change of changes) for (const node of change.addedNodes) {
      if (!(node instanceof HTMLTrackElement)) continue;
      const video = node.parentElement;
      const blob = blobs.get(node.src);
      const track = tracks.get(node);
      if (!(video instanceof HTMLVideoElement) || !blob || !track) continue;
      const update = () => {
        for (const [element, current] of tracks) if (element.parentElement === video) current.update(video.currentTime);
      };
      if (!listeners.has(video)) {
        listeners.set(video, update);
        for (const event of ['timeupdate', 'seeking', 'seeked', 'ratechange']) video.addEventListener(event, update);
      }
      const reader = new FileReader();
      reader.addEventListener('load', () => {
        track.load(String(reader.result)); track.update(video.currentTime); node.dispatchEvent(new Event('load'));
      });
      reader.readAsText(blob);
    }
  });
  observer.observe(document, { childList: true, subtree: true });
  return {
    createObjectURL, revokeObjectURL, tracks,
    seek: (video: HTMLVideoElement, seconds: number) => {
      video.currentTime = seconds; video.dispatchEvent(new Event('seeking'));
      video.dispatchEvent(new Event('timeupdate')); video.dispatchEvent(new Event('seeked'));
    },
    dispose: () => {
      observer.disconnect();
      for (const [video, update] of listeners) {
        for (const event of ['timeupdate', 'seeking', 'seeked', 'ratechange']) video.removeEventListener(event, update);
      }
      if (descriptor) Object.defineProperty(HTMLTrackElement.prototype, 'track', descriptor);
      else Reflect.deleteProperty(HTMLTrackElement.prototype, 'track');
      vi.unstubAllGlobals();
    },
  };
};

class SimulatedTextTrack extends EventTarget {
  mode: TextTrackMode = 'disabled';
  private cues: { start: number; end: number; text: string }[] = [];
  activeCues: { getCueAsHTML: () => DocumentFragment }[] = [];
  load(vtt: string) {
    this.cues = vtt.split('\n\n').flatMap((block) => {
      const lines = block.split('\n');
      const timing = lines.findIndex((line) => line.includes(' --> '));
      if (timing < 0) return [];
      const [start = '', end = ''] = (lines[timing] ?? '').split(' --> ');
      const seconds = (time: string) => time.split(':').reduce((total, part) => total * 60 + Number(part), 0);
      return [{ start: seconds(start), end: seconds(end), text: lines.slice(timing + 1).join('\n') }];
    });
  }
  update(seconds: number) {
    this.activeCues = this.mode === 'disabled' ? [] : this.cues.filter((cue) => cue.start <= seconds && seconds < cue.end)
      .map((cue) => ({ getCueAsHTML: () => {
        const fragment = document.createDocumentFragment();
        const text = cue.text.replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&amp;', '&');
        fragment.append(document.createTextNode(text)); return fragment;
      } }));
    this.dispatchEvent(new Event('cuechange'));
  }
}
