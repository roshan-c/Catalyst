// Decodes and type-checks the Effect Schema generated from TestDataExtended.
import { Schema } from 'effect';
import { Floor, Game, Heist, Crash, Phase } from '../output/ts-extended/Game';
import { Lobby } from '../output/ts-extended/Lobby';

const decodeFloor = Schema.decodeUnknownSync(Floor);

const floor = decodeFloor({
    games: [
        { kind: 'heist', crew: 3, minCrew: null },
        { kind: 'crash', phase: 'flying', multiplier: 2.5 },
    ],
    byName: { h: { kind: 'heist', crew: 1, minCrew: 4, closesAt: 10 } },
    phases: ['betting', 'crashed-out'],
});

// The discriminator narrows the union.
const first: Game = floor.games[0]!;
if (first.kind === 'heist') {
    const crew: number = first.crew;
    const minCrew: number | null = first.minCrew;
    void crew; void minCrew;
}
const second = floor.games[1]!;
if (second.kind !== 'crash' || second.startPhase !== Phase.Crashed)
    throw new Error('crash should default startPhase to crashed-out');

const mustFail = (label: string, value: unknown, schema: Schema.Schema<any, any>) => {
    let failed = false;
    try { Schema.decodeUnknownSync(schema)(value); } catch { failed = true; }
    if (!failed) throw new Error(`${label} should fail to decode`);
};
mustFail('unknown kind', { kind: 'nope' }, Game);
mustFail('missing nullable key', { kind: 'heist', crew: 1 }, Heist);
mustFail('null optional', { kind: 'crash', phase: 'flying', multiplier: null }, Crash);
mustFail('bad string enum', { kind: 'crash', phase: 'landed' }, Crash);

Schema.decodeUnknownSync(Lobby)({
    floor: { games: [], byName: {}, phases: [] },
    spotlight: { kind: 'crash', phase: 'betting' },
});
console.log('TypeScript extended checks passed');
