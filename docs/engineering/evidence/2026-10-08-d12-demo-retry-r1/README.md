# Paused construction retry correction

2026-10-08. Real r3 clearance checkpoint, already paused, active construction. Source red reproduced stale cancellation/timeout Reason after retry; old FailGround handler repeated the assertion until manually stopped (exit130), not a passing run. Minimal correction assigns a fresh reason in RetryBaseBuild after selecting a valid stage; duplicate equal status text is omitted. DemoCheck now requests exit1 before raising so failing checks cannot repeat indefinitely.

Green source runner: nine independent processes PASS/exit0; retry checks current reason and unchanged actual ledger containers while paused. Integrated UI126 checks/zero failures/SaveQuit exit0; build0 warnings/errors. Initial source invocation lacked DOTNET_ROOT and aborted before any check; preserved privately, explicit SDK environment rerun passed. This is source evidence, not r5 or r6 GUI qualification.
