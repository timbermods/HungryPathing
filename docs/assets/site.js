// Fills in the newest release version and direct download link. The page works without it:
// every download link already points at the releases page. Pre-releases count, because an alpha
// is all this mod has so far; the version chip says "preview" when that is what it found.
(function () {
  var repo = "timbermods/HungryPathing";
  var key = "hp-latest-release";

  function apply(release) {
    if (!release || !release.tag_name) return;
    document.querySelectorAll("[data-latest-version]").forEach(function (el) {
      el.textContent = release.tag_name + (release.prerelease ? " preview" : "");
      el.hidden = false;
    });
    var zip = (release.assets || []).filter(function (a) {
      return /^HungryPathing-[\d.]+\.zip$/.test(a.name);
    })[0];
    if (zip) {
      document.querySelectorAll("[data-latest-zip]").forEach(function (el) {
        el.href = zip.browser_download_url;
      });
      document.querySelectorAll("[data-zip-name]").forEach(function (el) { el.textContent = zip.name; });
      document.querySelectorAll("[data-zip-line]").forEach(function (el) { el.hidden = false; });
    }
  }

  var cached = null;
  try { cached = JSON.parse(sessionStorage.getItem(key)); } catch (e) {}
  if (cached) { apply(cached); return; }

  fetch("https://api.github.com/repos/" + repo + "/releases?per_page=10", { headers: { Accept: "application/vnd.github+json" } })
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (releases) {
      if (!releases || !releases.length) return;
      var release = releases.filter(function (r) { return !r.draft; })[0];
      if (!release) return;
      var slim = {
        tag_name: release.tag_name,
        prerelease: !!release.prerelease,
        assets: (release.assets || []).map(function (a) { return { name: a.name, browser_download_url: a.browser_download_url }; })
      };
      try { sessionStorage.setItem(key, JSON.stringify(slim)); } catch (e) {}
      apply(slim);
    })
    .catch(function () {});
})();
