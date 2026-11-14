# Working rules

## Phase

We are in the bug-fixing phase. Parts 0 to 12 of `docs/PLAN.md` are built and reviewed. I'm testing the whole application and reviewing the code, and Part 13 (documentation) starts only when bug fixing is done. Fix the bugs and issues I report, one at a time, each with a test that fails before the fix, and wait for my feedback between them.

`docs/DESIGN.md` is the agreed design: build what it says. If the design is silent, unclear or turns out to be wrong, stop and ask (see Context) instead of deciding on your own. When we change a decision, update `docs/DESIGN.md` too.

Work from `docs/PLAN.md`:

- Don't write code until I've approved the plan.
- Build one part at a time. Don't start the next part until I've reviewed the current one.
- When a part is finished, update its entry in `docs/PLAN.md`: mark it Built, and record what was built, each test case, and which BR or OP each test covers. Include the mutation checks: the code you broke on purpose and the test that failed.
- Don't create files that the plan doesn't call for without asking me first.

## Tests

- Follow Arrange / Act / Assert, with `// Arrange`, `// Act` and `// Assert` comments.
- Name .NET tests `Given_<State>_When_<Action>_Then_<Outcome>`. Name Vitest tests `given <state>, when <action>, then <outcome>`.
- Test one behaviour per test. Use `[Theory]` for the boundaries of a single rule.
- Never use the real clock, the machine's time zone or shared state. Tests must pass in any order and in parallel.
- Every BR or OP test must fail when its rule is broken. Prove it with a mutation check before marking a part Built.

## Context

Please read the `docs\CONTEXT.md` to understand what is the project is about.
When the context file doesn’t cover something, list the options with trade-offs and your recommendation, then ask me to decide.

## Correction log

When I correct you or reject a suggestion, append an entry to `docs/AI_LOG.md` with:

- Date
- What you proposed
- What was wrong
- What we changed

Keep it factual. Don't soften your own mistakes.

When I say ‘log session’, append a short summary: what we worked on, decisions made, and the prompts that mattered.

## Prompt log

Append every prompt I send you to the "Prompt log" section of `docs/AI_LOG.md`, verbatim, with the date and time. Do it at the start of each turn, before anything else. Don't summarise, correct or reformat the prompt.

## Considerations and limitations

When I ask you to record an assumption, a consideration, a limitation or something to revisit, add it to the "Considerations and limitations" section at the end of `docs/DESIGN.md`, with the situation, the decision, and why. `docs/AI_LOG.md` holds logs only: corrections, prompts and session summaries.

## Git

Never commit or push. I commit.
