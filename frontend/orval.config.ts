import { defineConfig } from 'orval';

// Generates a typed client + React Query hooks from the API's OpenAPI spec.
//
// Reads a committed snapshot (./openapi.json) so `pnpm gen:api` works on a fresh clone. To
// regenerate from the LIVE backend, start the API and run:
//   curl -s http://localhost:5080/openapi/v1.json -o openapi.json && pnpm gen:api
export default defineConfig({
  amenities: {
    input: './openapi.json',
    output: {
      mode: 'single',
      target: './src/api/generated/api.ts',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        // Return the parsed body directly (e.g. Amenity[]) rather than a {data,status,headers}
        // wrapper — simpler to consume, and it matches what our fetcher actually returns.
        fetch: { includeHttpResponseReturnType: false },
        // All requests go through our fetcher so identity headers are injected in one place.
        mutator: {
          path: './src/api/fetcher.ts',
          name: 'customFetch',
        },
      },
    },
  },
});
