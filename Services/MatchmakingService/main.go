package main

import (
	"encoding/json"
	"log"
	"math/rand/v2"
	"net/http"
	"os"
	"time"
)

type MatchTicket struct {
	TicketID  string `json:"ticketId"`
	Region   string `json:"region"`
	Mode     string `json:"mode"`
	Estimate string `json:"estimate"`
}

func main() {
	mux := http.NewServeMux()
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		writeJSON(w, map[string]string{"status": "healthy", "service": "matchmaking-api"})
	})
	mux.HandleFunc("/match", func(w http.ResponseWriter, r *http.Request) {
		mode := r.URL.Query().Get("mode")
		if mode == "" {
			mode = "classic"
		}

		writeJSON(w, MatchTicket{
			TicketID:  time.Now().UTC().Format("20060102150405") + "-" + randomSuffix(),
			Region:   "eastus",
			Mode:     mode,
			Estimate: "under 10 seconds",
		})
	})

	port := os.Getenv("PORT")
	if port == "" {
		port = "8081"
	}

	log.Printf("matchmaking-api listening on :%s", port)
	log.Fatal(http.ListenAndServe(":"+port, mux))
}

func writeJSON(w http.ResponseWriter, value any) {
	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(value); err != nil {
		http.Error(w, err.Error(), http.StatusInternalServerError)
	}
}

func randomSuffix() string {
	const alphabet = "abcdefghijklmnopqrstuvwxyz0123456789"
	bytes := make([]byte, 6)
	for i := range bytes {
		bytes[i] = alphabet[rand.IntN(len(alphabet))]
	}
	return string(bytes)
}
