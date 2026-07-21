import { createServer, type ServerResponse } from "node:http";

const port = Number(process.env.PORT ?? 8082);

const scoreApiUrl =
  process.env.SCORE_API_URL ??
  process.env.services__score_api__http__0 ??
  process.env.services__score_api__https__0;

type ScoreEntry = {
  player: string;
  points: number;
  mode: string;
  playedAt: string;
};

const fallbackScores: ScoreEntry[] = [
  { player: "Maddy", points: 42, mode: "classic", playedAt: new Date().toISOString() },
  { player: "Clint", points: 37, mode: "xbox-context", playedAt: new Date().toISOString() },
  { player: "Nick", points: 35, mode: "godot-demo", playedAt: new Date().toISOString() }
];

const server = createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "localhost"}`);

  if (url.pathname === "/health") {
    return writeJson(response, 200, { status: "healthy", service: "leaderboard-api" });
  }

  if (url.pathname === "/leaderboard") {
    const scores = await loadScores();
    return writeJson(response, 200, {
      generatedAt: new Date().toISOString(),
      source: scoreApiUrl ? "score-api" : "local-fallback",
      leaders: scores.sort((a, b) => b.points - a.points).slice(0, 5)
    });
  }

  writeJson(response, 404, { error: "Not found" });
});

server.listen(port, () => {
  console.log(`leaderboard-api listening on :${port}`);
});

async function loadScores(): Promise<ScoreEntry[]> {
  if (!scoreApiUrl) {
    return fallbackScores;
  }

  const response = await fetch(`${scoreApiUrl}/scores`);
  if (!response.ok) {
    throw new Error(`Score API returned ${response.status}`);
  }

  return (await response.json()) as ScoreEntry[];
}

function writeJson(response: ServerResponse, status: number, body: unknown): void {
  response.writeHead(status, { "Content-Type": "application/json" });
  response.end(JSON.stringify(body));
}
