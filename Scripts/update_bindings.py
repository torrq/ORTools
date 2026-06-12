import os
import re

directory = r"C:\Users\nathan\Projects\RO\ORTools\ORTools.UI\Views"

for root, _, files in os.walk(directory):
    for filename in files:
        if filename.endswith(".xaml"):
            filepath = os.path.join(root, filename)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Find all <TextBox ... /> or <TextBox ... >
            # We want to match the whole tag
            new_content = content
            
            def replacer(match):
                tag = match.group(0)
                if 'Style="{StaticResource KeyTextBoxStyle}"' in tag:
                    # add converter if not present
                    if 'Converter={StaticResource KeyToStringConverter}' not in tag:
                        # find the Text Binding
                        tag = re.sub(r'Text="\{Binding ([^}]+)\}"', r'Text="{Binding \1, Converter={StaticResource KeyToStringConverter}}"', tag)
                return tag
                
            new_content = re.sub(r'<TextBox[^>]*>', replacer, new_content)
            
            if new_content != content:
                print(f"Updated {filepath}")
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(new_content)
