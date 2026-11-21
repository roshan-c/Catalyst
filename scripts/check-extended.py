"""Round-trips the Python generated from TestDataExtended into ./output/py_extended."""
import json
import sys

sys.path.insert(0, "output")

from py_extended.game import Crash, Floor, Heist, Phase, game_from_dict  # noqa: E402
from py_extended.lobby import Lobby  # noqa: E402

heist = Heist(crew=3, min_crew=None)
crash = Crash(phase=Phase.FLYING, multiplier=2.5)

# presence: optional keys are left out when None, nullable keys are always written.
assert heist.to_dict() == {"kind": "heist", "crew": 3, "minCrew": None}, heist.to_dict()
assert crash.to_dict() == {"kind": "crash", "phase": "flying", "multiplier": 2.5, "startPhase": "crashed-out"}, crash.to_dict()

# string enums go over the wire as their string value.
assert Phase("crashed-out") is Phase.CRASHED
assert Crash.from_dict({"phase": "betting"}).start_phase is Phase.CRASHED

# a nullable key is required on the way in.
try:
    Heist.from_dict({"crew": 1})
except KeyError:
    pass
else:
    raise AssertionError("a missing nullable key should fail")

# unions dispatch on the discriminator, in lists, maps and optionals.
floor = Floor(games=[heist, crash], featured=crash, by_name={"h": heist}, phases=[Phase.BETTING])
wire = json.loads(json.dumps(floor.to_dict()))
assert [g["kind"] for g in wire["games"]] == ["heist", "crash"]
again = Floor.from_dict(wire)
assert again == floor, (again, floor)
assert isinstance(game_from_dict({"kind": "crash", "phase": "betting"}), Crash)
try:
    game_from_dict({"kind": "nope"})
except ValueError:
    pass
else:
    raise AssertionError("an unknown kind should fail")

lobby = Lobby(floor=floor, spotlight=heist, phase=None)
assert Lobby.from_dict(json.loads(lobby.to_json())) == lobby
print("Python extended checks passed")
