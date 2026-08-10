import { defineConfig } from 'orval';

// Generates a typed client from the API's OpenAPI spec.
//
// By default we read a committed snapshot (./openapi.json) so `pnpm gen:api` works
// even before the backend is running. To regenerate from the LIVE backend instead
// (recommended once you add endpoints), start the API and point `input` at:
//   http://localhost:5080/openapi/v1.json
export default defineConfig({
  amenities: {
    input: './openapi.json',
    output: {
      mode: 'single',
      target: './src/api/generated/api.ts',
      client: 'fetch',
      clean: true,
      override: {
        // Return the parsed response body directly (e.g. Amenity[]) rather than a
        // wrapper object — simpler to consume from components.
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
});
