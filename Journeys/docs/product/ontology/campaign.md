# Ontology: campaign

A **campaign** is an executable program: journey graph + rules + outcomes, with Draft/Live lifecycle and idempotent event processing.

It is not a spreadsheet row, a chatbot transcript, or a prompt-only JSON blob. The campaign agent authors through the **same APIs** the engine uses to process production events.

Processed events persist on the event wrapper using the canonical lowercase symbols in `docs/product/ontology/event-model.md` (`appliedcampaigns`, `outcomestates`, and the rest of the `*AndRuleState` contract).
