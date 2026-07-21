package main

import (
	"context"
	"crypto/rand"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"strings"
	"sync"
	"time"

	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/sdk/resource"
	sdktrace "go.opentelemetry.io/otel/sdk/trace"
)

type MatchTicket struct {
	TicketID string `json:"ticketId"`
	Mode     string `json:"mode"`
	Player   string `json:"player"`
	Status   string `json:"status"`
	Opponent string `json:"opponent,omitempty"`
}

type MatchRequest struct {
	Player string `json:"player"`
	Mode   string `json:"mode"`
}

func main() {
	shutdownTelemetry, err := configureTelemetry(context.Background())
	if err != nil {
		log.Fatalf("configuring OpenTelemetry: %v", err)
	}
	defer func() {
		if err := shutdownTelemetry(context.Background()); err != nil {
			log.Printf("shutting down OpenTelemetry: %v", err)
		}
	}()

	matchmaker := newMatchmaker()
	scoreAPIURL := strings.TrimSuffix(os.Getenv("SCORE_API_URL"), "/")
	scoreAPIClient := &http.Client{
		Timeout:   5 * time.Second,
		Transport: otelhttp.NewTransport(http.DefaultTransport),
	}
	mux := http.NewServeMux()
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		writeJSON(w, map[string]string{"status": "healthy", "service": "matchmaking-api"})
	})
	mux.HandleFunc("/match", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "POST is required.", http.StatusMethodNotAllowed)
			return
		}

		var request MatchRequest
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			http.Error(w, "Invalid match request.", http.StatusBadRequest)
			return
		}

		ticket, err := matchmaker.join(request)
		if err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}

		writeJSON(w, ticket)
	})
	mux.HandleFunc("/match/", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, "GET is required.", http.StatusMethodNotAllowed)
			return
		}

		ticket, ok := matchmaker.get(strings.TrimPrefix(r.URL.Path, "/match/"))
		if !ok {
			http.Error(w, "Match ticket not found.", http.StatusNotFound)
			return
		}

		writeJSON(w, ticket)
	})
	mux.HandleFunc("/demo-opponent", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "POST is required.", http.StatusMethodNotAllowed)
			return
		}

		var request MatchRequest
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			http.Error(w, "Invalid demo opponent request.", http.StatusBadRequest)
			return
		}

		ticket, err := matchmaker.addDemoOpponent(request)
		if err != nil {
			http.Error(w, err.Error(), http.StatusConflict)
			return
		}

		if err := seedDemoScores(r.Context(), scoreAPIClient, scoreAPIURL); err != nil {
			http.Error(w, err.Error(), http.StatusBadGateway)
			return
		}

		writeJSON(w, ticket)
	})

	port := os.Getenv("PORT")
	if port == "" {
		port = "8081"
	}

	log.Printf("matchmaking-api listening on :%s", port)
	log.Fatal(http.ListenAndServe(":"+port, otelhttp.NewHandler(mux, "matchmaking-api")))
}

func writeJSON(w http.ResponseWriter, value any) {
	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(value); err != nil {
		http.Error(w, err.Error(), http.StatusInternalServerError)
	}
}

func configureTelemetry(ctx context.Context) (func(context.Context) error, error) {
	if os.Getenv("OTEL_EXPORTER_OTLP_ENDPOINT") == "" {
		return func(context.Context) error { return nil }, nil
	}

	exporter, err := otlptracegrpc.New(ctx)
	if err != nil {
		return nil, err
	}

	provider := sdktrace.NewTracerProvider(
		sdktrace.WithBatcher(exporter),
		sdktrace.WithResource(resource.Default()),
	)
	otel.SetTracerProvider(provider)
	return provider.Shutdown, nil
}

func seedDemoScores(ctx context.Context, client *http.Client, scoreAPIURL string) error {
	if scoreAPIURL == "" {
		return fmt.Errorf("score API URL is not configured")
	}

	request, err := http.NewRequestWithContext(ctx, http.MethodPost, scoreAPIURL+"/demo-scores", nil)
	if err != nil {
		return fmt.Errorf("creating demo score request: %w", err)
	}

	response, err := client.Do(request)
	if err != nil {
		return fmt.Errorf("seeding demo scores: %w", err)
	}
	defer response.Body.Close()

	if response.StatusCode < http.StatusOK || response.StatusCode >= http.StatusMultipleChoices {
		return fmt.Errorf("score API returned %s while seeding demo scores", response.Status)
	}

	return nil
}

type matchmaker struct {
	mu      sync.Mutex
	tickets map[string]MatchTicket
	queues  map[string][]string
}

func newMatchmaker() *matchmaker {
	return &matchmaker{
		tickets: make(map[string]MatchTicket),
		queues:  make(map[string][]string),
	}
}

func (m *matchmaker) join(request MatchRequest) (MatchTicket, error) {
	m.mu.Lock()
	defer m.mu.Unlock()

	return m.joinLocked(request)
}

func (m *matchmaker) joinLocked(request MatchRequest) (MatchTicket, error) {
	player := strings.TrimSpace(request.Player)
	if player == "" {
		return MatchTicket{}, fmt.Errorf("player is required")
	}

	mode := strings.TrimSpace(request.Mode)
	if mode == "" {
		mode = "classic"
	}

	for _, ticket := range m.tickets {
		if ticket.Player == player && ticket.Mode == mode && ticket.Status == "queued" {
			return ticket, nil
		}
	}

	queue := m.queues[mode]
	for len(queue) > 0 {
		opponentID := queue[0]
		queue = queue[1:]
		opponent := m.tickets[opponentID]
		if opponent.Status != "queued" || opponent.Player == player {
			continue
		}

		ticket := MatchTicket{
			TicketID: newTicketID(),
			Mode:     mode,
			Player:   player,
			Status:   "matched",
			Opponent: opponent.Player,
		}
		opponent.Status = "matched"
		opponent.Opponent = player
		m.tickets[opponent.TicketID] = opponent
		m.tickets[ticket.TicketID] = ticket
		m.queues[mode] = queue
		return ticket, nil
	}

	ticket := MatchTicket{
		TicketID: newTicketID(),
		Mode:     mode,
		Player:   player,
		Status:   "queued",
	}
	m.tickets[ticket.TicketID] = ticket
	m.queues[mode] = append(queue, ticket.TicketID)
	return ticket, nil
}

func (m *matchmaker) addDemoOpponent(request MatchRequest) (MatchTicket, error) {
	player := strings.TrimSpace(request.Player)
	if player == "" {
		return MatchTicket{}, fmt.Errorf("player is required")
	}

	mode := strings.TrimSpace(request.Mode)
	if mode == "" {
		mode = "classic"
	}

	m.mu.Lock()
	defer m.mu.Unlock()

	for _, ticket := range m.tickets {
		if ticket.Player == player &&
			ticket.Mode == mode &&
			ticket.Status == "matched" &&
			ticket.Opponent == "Aspire Demo Bot" {
			return ticket, nil
		}
	}

	queue := m.queues[mode]
	playerTicketIndex := -1
	for index, ticketID := range queue {
		ticket := m.tickets[ticketID]
		if ticket.Player == player && ticket.Status == "queued" {
			playerTicketIndex = index
			break
		}
	}

	if playerTicketIndex == -1 {
		return MatchTicket{}, fmt.Errorf("%s is not waiting for a %s match", player, mode)
	}

	playerTicketID := queue[playerTicketIndex]
	playerTicket := m.tickets[playerTicketID]
	demoTicket := MatchTicket{
		TicketID: newTicketID(),
		Mode:     mode,
		Player:   "Aspire Demo Bot",
		Status:   "matched",
		Opponent: player,
	}
	playerTicket.Status = "matched"
	playerTicket.Opponent = demoTicket.Player

	m.queues[mode] = append(queue[:playerTicketIndex], queue[playerTicketIndex+1:]...)
	m.tickets[playerTicketID] = playerTicket
	m.tickets[demoTicket.TicketID] = demoTicket
	return playerTicket, nil
}

func (m *matchmaker) get(ticketID string) (MatchTicket, bool) {
	m.mu.Lock()
	defer m.mu.Unlock()

	ticket, ok := m.tickets[ticketID]
	return ticket, ok
}

func newTicketID() string {
	var bytes [8]byte
	if _, err := rand.Read(bytes[:]); err != nil {
		panic(fmt.Errorf("creating match ticket ID: %w", err))
	}

	return fmt.Sprintf("%d-%x", time.Now().UTC().UnixMilli(), bytes)
}
