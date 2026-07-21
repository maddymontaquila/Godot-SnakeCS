import Reveal from "reveal.js";
import RevealHighlight from "reveal.js/plugin/highlight/highlight.esm.js";
import RevealNotes from "reveal.js/plugin/notes/notes.esm.js";
import "reveal.js/dist/reveal.css";
import "../styles.css";

Reveal.initialize({
  hash: true,
  controls: true,
  progress: true,
  center: false,
  width: 1600,
  height: 900,
  margin: 0.04,
  transition: "fade",
  backgroundTransition: "fade",
  pdfSeparateFragments: false,
  plugins: [RevealNotes, RevealHighlight]
});
