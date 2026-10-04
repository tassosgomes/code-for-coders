export const setupPlaybackRequest = (xhr: XMLHttpRequest, url: string, query: string) => {
  const resource = new URL(url, window.location.href);
  // Only segment objects receive the opaque credential; playlists and keys use the BFF cookie.
  if (/\/hls\/(480p|720p|1080p)\/segment_[0-9]+\.ts$/.test(resource.pathname)) {
    xhr.open('GET', resource.href + (resource.search ? '&' : '?') + query.replace(/^\?/, ''), true);
    xhr.withCredentials = false;
  } else {
    xhr.withCredentials = resource.origin === window.location.origin;
  }
};
