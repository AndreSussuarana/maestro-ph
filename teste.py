import subprocess
import os
from fastapi import FastAPI, UploadFile, File, HTTPException
import cv2 
import numpy as np

app = FastAPI(
    title="Maestro PH - Visão Computacional & C#",
    description="API que lê fitas de ph por foto e ordena o histórico com um motor C#"
)

# Caminho do executável C# na mesma pasta do script Python
CAMINHO_EXE = os.path.join(os.getcwd(), "POO csharp.exe")


def rgb_para_hex(r, g, b):
    """ Transforma valores RGB em uma string Hexadecimal (#HEX) """
    return f"#{r:02x}{g:02x}{b:02x}"


def chamar_ordenacao_csharp(dados_para_envio: str):
    """Chama o executável de C# passando a string de dados e tratando erros"""
    if not os.path.exists(CAMINHO_EXE):
        return "Erro: Executável C# não encontrado na pasta.", "-1"

    try:
        resultado = subprocess.run(
            [CAMINHO_EXE, dados_para_envio],
            capture_output=True,
            text=True,
            encoding='utf-8',
            timeout=5
        )
        
        if resultado.returncode == 0 and '|' in resultado.stdout:
            partes = resultado.stdout.strip().split('|')
            return partes[0], partes[1]
        else:
            erro_csharp = resultado.stderr.strip() or "Erro desconhecido no motor C#"
            return f"Erro no C#: {erro_csharp}", "-1"

    except subprocess.TimeoutExpired:
        return "Erro: O processamento C# demorou demais.", "-1"
    except Exception as e:
        return f"Erro inesperado: {str(e)}", "-1"


def processar_fita_4_zonas(imagem_bytes):
    """ Abre a foto da fita, faz um resize de segurança, fatia em 4 e extrai o RGB """
    # Converte bytes da requisição para matriz OpenCV
    nparr = np.frombuffer(imagem_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    
    if img is None:
        raise ValueError("Arquivo enviado não é uma imagem válida.")

    # BLINDAGEM CONTRA FOTOS GIGANTES: Redimensiona para um tamanho leve e padrão
    img = cv2.resize(img, (400, 600), interpolation=cv2.INTER_AREA)

    # Converte de BGR para RGB
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    
    altura, largura, _ = img_rgb.shape
    altura_quadrado = altura // 4
    cores_extraidas = []

    # Fatia verticalmente em 4 blocos
    for i in range(4):
        y_inicial = i * altura_quadrado
        y_final = (i + 1) * altura_quadrado
        
        zona = img_rgb[y_inicial:y_final, 0:largura]
        media_cor = zona.mean(axis=0).mean(axis=0)
        
        cores_extraidas.append((int(media_cor[0]), int(media_cor[1]), int(media_cor[2])))
        
    return cores_extraidas


@app.post("/analisar-fita")
async def analisar_fita_ph(file: UploadFile = File(...)):
    """
    Recebe uma foto da fita de PH, processa as 4 zonas de cores e aciona o motor C#
    """
    if not file.filename.lower().endswith(('.png', '.jpg', '.jpeg')):
        raise HTTPException(status_code=400, detail="Formato inválido, envie uma imagem (.png, .jpg, .jpeg)")
    
    try:
        # Lê o arquivo direto da memória (sem salvar arquivo temporário no HD!)
        conteudo_imagem = await file.read()
        
        # Extrai as 4 cores reais da imagem enviada pelo usuário
        cores = processar_fita_4_zonas(conteudo_imagem)
        c1, c2, c3, c4 = cores[0], cores[1], cores[2], cores[3]

        # Monta a string de cores que o cozinheiro C# espera ler
        argumento_cores = f"{c1[0]},{c1[1]},{c1[2]}|{c2[0]},{c2[1]},{c2[2]}|{c3[0]},{c3[1]},{c3[2]}|{c4[0]},{c4[1]},{c4[2]}"

        # Manda para o C# processar a matemática da caixinha e ordenar
        resultado_csharp, indice_neutro = chamar_ordenacao_csharp(argumento_cores)

        if indice_neutro == "-1":
            raise HTTPException(status_code=500, detail=resultado_csharp)

        # Retorna o JSON ultra interativo para o usuário
        return {
            "sucesso": True,
            "ph_final_detectado": int(resultado_csharp), # O C# devolve o pH calculado
            "posicao_do_neutro_no_ranking": int(indice_neutro),
            "cores_da_fita_processadas": [
                {"zona": 1, "hex": rgb_para_hex(*c1)},
                {"zona": 2, "hex": rgb_para_hex(*c2)},
                {"zona": 3, "hex": rgb_para_hex(*c3)},
                {"zona": 4, "hex": rgb_para_hex(*c4)}
            ]
        }

    except ValueError as val_err:
        raise HTTPException(status_code=400, detail=str(val_err))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Erro interno no servidor: {str(e)}")


if __name__ == "__main__":
    print("--- Teste de Integração Direta do Motor ---")
    # Testando o envio de uma string de 4 cores mockadas para ver se o C# responde
    exemplo_cores = "230,50,50|230,150,30|180,210,50|30,90,150"
    ordenado, neutro = chamar_ordenacao_csharp(exemplo_cores)
    print(f"Resposta do C#: {ordenado} | Posição Neutro: {neutro}\n")
    print("Para rodar a API de verdade, use no terminal: uvicorn nome_do_seu_arquivo:app --reload")