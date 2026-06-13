import os
import re

def update_xaml_files(directory):
    # Matches {StaticResource App...Brush} or {StaticResource App...} (assuming all App* resources are dynamic theme elements)
    # The regex targets "{StaticResource App" and replaces it with "{DynamicResource App"
    pattern = re.compile(r'\{StaticResource App([A-Za-z]+Brush)\}')
    count = 0
    
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith('.xaml'):
                filepath = os.path.join(root, file)
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                new_content, num_subs = pattern.subn(r'{DynamicResource App\1}', content)
                
                if num_subs > 0:
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(new_content)
                    print(f"Updated {file} ({num_subs} replacements)")
                    count += num_subs

    print(f"\nTotal replacements: {count}")

if __name__ == '__main__':
    ui_path = os.path.join(os.path.dirname(__file__), '..', 'ORTools.UI')
    update_xaml_files(ui_path)
