// The page blocked websites go to. It names the app whose own limit is used up; without one, the daily total is.
const app = new URLSearchParams(location.search).get("app");
if (app) {
  document.title = `Time's up for ${app}`;
  document.getElementById("title")!.textContent = `Time's up for ${app} today.`;
  document.getElementById("detail")!.textContent =
    "Other websites still work while you have time left.";
}
