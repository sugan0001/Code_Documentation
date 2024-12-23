import os
import tiktoken

MAX_TOKENS = 128000

def num_tokens_from_string(string: str, encoding_name: str) -> int:
    encoding = tiktoken.get_encoding(encoding_name)
    num_tokens = len(encoding.encode(string))
    return num_tokens

def count_tokens_in_directory(directory_path: str, encoding_name: str) -> int:
    total_tokens = 0  

    for root, dirs, files in os.walk(directory_path):
        for file_name in files:
            file_path = os.path.join(root, file_name)
            try:
                with open(file_path, 'r', encoding='utf-8') as file:
                    code_text = file.read()
                
                token_length = num_tokens_from_string(code_text, encoding_name)
                total_tokens += token_length  
            
            except FileNotFoundError:
                print(f"File not found: {file_path}")
            except PermissionError:
                print(f"Permission denied: {file_path}. Please check file permissions.")
            except Exception as e:
                print(f"An error occurred with file {file_path}: {e}")

    return total_tokens

def check_token_limit(directory_path: str, encoding_name: str) -> bool:
    total_tokens = count_tokens_in_directory(directory_path, encoding_name)
    print(f'Total number of tokens across all files: {total_tokens}')
    
    if total_tokens > MAX_TOKENS:
        return False  
    return True  
