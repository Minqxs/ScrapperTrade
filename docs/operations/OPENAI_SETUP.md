# Optional OpenAI research provider

ScrapperTrade works without an OpenAI API key through its manual ChatGPT research workflow. OpenAI API billing is separate from a ChatGPT subscription.

If the optional provider is enabled later, first revoke any key that has been pasted into chat, logs, screenshots, or source control. Create a replacement project key and expose it only to the local host process as `OPENAI_API_KEY`; never place it in this repository, a committed `.env` file, browser storage, or the React application. Select the model explicitly through local configuration rather than hard-coding a moving default.

The provider uses the Responses API with strict JSON-schema output. Its response remains untrusted research data: it must pass schema, provenance, ambiguity, backtest, out-of-sample, robustness, shadow, and user-governance gates. The provider has no execution adapter, MT5 command port, permission mutation, risk-policy mutation, emergency unlock, promotion, or activation capability.

Official references: [API quickstart](https://developers.openai.com/api/docs/quickstart) and [structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
