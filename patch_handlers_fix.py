import re

with open('ORTools.Worker/WorkerCore.cs', 'r', encoding='utf-8') as f:
    content = f.read()

old_status_rec = """        foreach (var list in new[] { p.StatusRecovery.Panacea, p.StatusRecovery.RoyalJelly, p.StatusRecovery.GreenPotion })
        {
            if (list.Key == key) { list.Key = Keys.None; changed = true; }
        }"""
        
new_status_rec = """        foreach (var list in p.StatusRecovery.statusLists.Values)
        {
            if (list.Key == key) { list.Key = Keys.None; changed = true; }
        }"""

content = content.replace(old_status_rec, new_status_rec)

with open('ORTools.Worker/WorkerCore.cs', 'w', encoding='utf-8') as f:
    f.write(content)
