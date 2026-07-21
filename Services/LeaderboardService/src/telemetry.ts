import { getNodeAutoInstrumentations } from "@opentelemetry/auto-instrumentations-node";
import { OTLPMetricExporter } from "@opentelemetry/exporter-metrics-otlp-grpc";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-grpc";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { PeriodicExportingMetricReader } from "@opentelemetry/sdk-metrics";
import { NodeSDK } from "@opentelemetry/sdk-node";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";

const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;
const sdk = endpoint
  ? new NodeSDK({
      resource: resourceFromAttributes({
        [ATTR_SERVICE_NAME]: process.env.OTEL_SERVICE_NAME ?? "leaderboard-api"
      }),
      traceExporter: new OTLPTraceExporter({ url: endpoint }),
      metricReader: new PeriodicExportingMetricReader({
        exporter: new OTLPMetricExporter({ url: endpoint }),
        exportIntervalMillis: 5_000
      }),
      instrumentations: [
        getNodeAutoInstrumentations({
          "@opentelemetry/instrumentation-fs": { enabled: false }
        })
      ]
    })
  : undefined;

sdk?.start();

process.once("SIGTERM", () => {
  void sdk?.shutdown().finally(() => process.exit(0));
});
