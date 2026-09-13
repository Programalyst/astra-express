import ast
import json
from pathlib import Path
import runpy
import sys


project = Path(__file__).resolve().parent.parent
coach = ast.parse((project / 'server/coach_server.py').read_text())
rules = next(ast.literal_eval(node.value) for node in coach.body
             if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == 'RULES' for target in node.targets))
schema_node = next(node for node in coach.body if isinstance(node, ast.FunctionDef) and node.name == 'advice_schema')
advice_schema = ast.literal_eval(schema_node.body[0].value)
planner = runpy.run_path(str(project / 'server/astrabot_planner.py'))
contract = {'rules': rules, 'plannerRules': planner['PLANNER_RULES'],
            'adviceSchema': advice_schema, 'planSchema': planner['plan_schema']()}
output = 'window.astraBotContract = ' + json.dumps(contract, ensure_ascii=True, indent=2) + ';\n'
if '--check' in sys.argv:
    target = project / 'Assets/WebGLTemplates/Astra/astrabot-contract.js'
    if target.read_text() != output:
        raise SystemExit('Browser contract differs from the Python rules/schema. Regenerate it with export-astrabot-contract.py.')
    print('Browser rules and schemas match the Python contract.')
else:
    print(output, end='')
