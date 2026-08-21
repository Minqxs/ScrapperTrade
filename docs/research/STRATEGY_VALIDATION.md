# Strategy validation

Strategies are hypotheses. Promotion requires schema and semantic validation, cost-aware historical testing, separated out-of-sample evidence, walk-forward and sensitivity analysis, stress tests, sufficient sample size, shadow forward testing, and explicit user governance. Win rate alone is never an acceptance metric.

The deterministic validation engine models spread, adverse entry/exit slippage, and round-trip commission in quote-price units. Signals formed at a candle close enter at the next candle open. Gaps through stops receive the worse opening price, and bars touching stop and target resolve to the stop. These are conservative bar-data semantics, not a claim that historical fills predict live fills.

Reported evidence includes net and gross R, explicit cost R, expectancy, profit factor, drawdown, losing streak, a Sharpe-like statistic, and exposure. Chronological train/test splits require an embargo bar. Walk-forward folds select parameters only from their training window and evaluate the selection on the later out-of-sample window. Default safeguards reject insufficient training or out-of-sample trade counts, weak out-of-sample expectancy, excessive drawdown, and parameter grids whose neighboring hypotheses are unstable.

`BuiltInStrategyHypotheses.EmaTrendGrid` contains research hypotheses only. Passing engineering thresholds does not establish profitability or authorize runtime promotion; shadow forward testing and user governance remain separate gates.
