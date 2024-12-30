# Cosmos Failure Forcing POC

This is demonstrating how to inject an HTTP backend interceptor into the CosmosClient which allows us to establish
rules to trigger 429s, 404s, timeouts, etc on demand. 
Intended to aid in intergration testing of failure paths.

I'm using a `.env` file to specifically show how to do it via environment variables (can also be done easily via json config - but the imminent example would have us pass it via env vars).