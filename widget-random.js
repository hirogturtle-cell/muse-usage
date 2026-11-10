// Muse Usage Widget for Scriptable (iOS)
// Setup:
//   1. Paste the raw URL of your usage.json (e.g. a GitHub Gist raw URL)
//      into DATA_URL below.
//   2. Add this script in Scriptable, then add a small/medium Scriptable
//      widget to the Home Screen and select this script.
// The JSON is produced on the Muse VM by update-usage.sh, e.g.:
//   { "plan": "Power", "usedPercent": 25, "remainingPercent": 75,
//     "resetText": "Nov 12 at 7:24 PM JST",
//     "resetAt": "2026-11-12T19:24:00+09:00",
//     "updatedAt": "2026-11-09T15:24:00+09:00" }

// Yukari Edition: usage data is read from the GitHub Gist raw URL below.
// Paste your Gist raw URL (…/raw/usage.json) between the quotes.
const DATA_URL = "https://raw.githubusercontent.com/hirogturtle-cell/muse-usage/main/usage.json";

// RANDOM PHOTO MODE: photos are hosted as direct image files in the same
// GitHub repo (photos/photo-00.b64.jpg ... photo-62.b64.jpg). Despite the
// ".b64" in their names they are plain JPEG files. One is picked per day.
const PHOTO_BASE = "https://raw.githubusercontent.com/hirogturtle-cell/muse-usage/main/photos/";

function pad2(n) { return String(n).padStart(2, "0"); }

function photoIndexForToday() {
  const now = new Date();
  const dayKey = now.getFullYear() * 10000 + (now.getMonth() + 1) * 100 + now.getDate();
  return dayKey % 63;
}

async function fetchPhoto() {
  const fm = FileManager.local();
  try {
    const idx = photoIndexForToday();
    const name = `photo-${pad2(idx)}.b64.jpg`;
    const cachePath = fm.joinPath(fm.cacheDirectory(), name);
    if (!fm.fileExists(cachePath)) {
      const req = new Request(PHOTO_BASE + name);
      req.timeoutInterval = 15;
      const img = await req.loadImage();
      fm.writeImage(cachePath, img);
      return img;
    }
    return fm.readImage(cachePath);
  } catch (e) {
    return null;
  }
}

async function fetchUsage() {
  const fm = FileManager.local();
  const cachePath = fm.joinPath(fm.cacheDirectory(), "muse-usage.json");
  // PHOTO-ONLY MODE: no data URL configured -> use the built-in sample values
  // so the widget shows Yukari's photo without any network access.
  if (!DATA_URL) {
    return {
      plan: "Power",
      usedPercent: 31,
      remainingPercent: 69,
      resetText: "Nov 12 at 7:24 PM JST",
      resetAt: "2026-11-12T19:24:00+09:00",
      updatedAt: "2026-11-10T08:02:39+09:00"
    };
  }
  try {
    const req = new Request(DATA_URL);
    req.timeoutInterval = 10;
    const data = await req.loadJSON();
    fm.writeString(cachePath, JSON.stringify(data));
    return data;
  } catch (e) {
    // Offline etc.: fall back to the last cached copy.
    if (fm.fileExists(cachePath)) {
      return JSON.parse(fm.readString(cachePath));
    }
    throw e;
  }
}

const YUKARI_B64 = "";

function daysUntil(dateStr) {
  if (!dateStr) return null;
  const ms = new Date(dateStr).getTime() - Date.now();
  return Math.max(0, Math.ceil(ms / 86400000));
}

function buildStats(w, d) {
  const title = w.addText("Muse");
  title.font = Font.boldSystemFont(13);
  title.textColor = new Color("#9ca3af");

  w.addSpacer(4);

  const pct = w.addText(`${d.remainingPercent}% left`);
  pct.font = Font.boldSystemFont(26);
  pct.textColor = d.usedPercent >= 90 ? new Color("#f87171") : Color.white();

  const used = w.addText(`Weekly ${d.usedPercent}% used · ${d.plan || ""}`);
  used.font = Font.systemFont(11);
  used.textColor = new Color("#d1d5db");

  w.addSpacer(4);

  const days = daysUntil(d.resetAt);
  const resetLine = days !== null
    ? `Resets in ${days} day${days === 1 ? "" : "s"}`
    : `Resets ${d.resetText || ""}`;
  const reset = w.addText(resetLine);
  reset.font = Font.systemFont(11);
  reset.textColor = new Color("#9ca3af");

  const upd = w.addText(`Updated ${String(d.updatedAt || "").slice(5, 16).replace("T", " ")}`);
  upd.font = Font.systemFont(9);
  upd.textColor = new Color("#6b7280");
}

async function buildWidget(d) {
  const w = new ListWidget();
  w.backgroundColor = new Color("#111827");
  w.setPadding(12, 14, 12, 14);


  // LARGE: Yukari fills the widget; usage rides in a small bar at the bottom.
  if (config.widgetFamily === "large") {
    const photo = await fetchPhoto();
    if (photo) {
      w.backgroundImage = photo;
    } else {
      try {
        const bg = Image.fromData(Data.fromBase64String(YUKARI_B64));
        w.backgroundImage = bg;
      } catch (e) {}
    }
    w.setPadding(10, 12, 10, 12);
    w.addSpacer();
    const bar = w.addStack();
    bar.layoutHorizontally();
    bar.backgroundColor = new Color("#000000", 0.45);
    bar.cornerRadius = 8;
    bar.setPadding(6, 10, 6, 10);
    const t = bar.addText(`Muse  ${d.remainingPercent}% left \u00b7 Weekly ${d.usedPercent}% used \u00b7 ${d.plan || ""}`);
    t.font = Font.boldSystemFont(13);
    t.textColor = Color.white();
    return w;
  }

  // Yukari photo on the left; usage stats on the right (medium layout).
  if (config.widgetFamily !== "small") {
    const row = w.addStack();
    row.layoutHorizontally();
    const photoM = await fetchPhoto();
    try {
      const img = row.addImage(photoM || Image.fromData(Data.fromBase64String(YUKARI_B64)));
      img.imageSize = new Size(96, 144);
      img.cornerRadius = 8;
    } catch (e) {}
    row.addSpacer(12);
    const col = row.addStack();
    col.layoutVertically();
    buildStats(col, d);
    return w;
  }

  buildStats(w, d);

  // Ask iOS to refresh roughly every 15 minutes (iOS decides the real cadence).
  w.refreshAfterDate = new Date(Date.now() + 15 * 60 * 1000);
  return w;
}

const data = await fetchUsage();
const widget = await buildWidget(data);
if (config.runsInWidget) {
  Script.setWidget(widget);
} else {
  widget.presentLarge();
}
Script.complete();
