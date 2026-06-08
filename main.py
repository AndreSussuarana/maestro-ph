import subprocess
import os
from fastapi import FastAPI, UploadFile, File, HTTPException
import cv2 
import numpy as np
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI(
    title="Maestro PH - Visão Computacional & C#",
    description="API que lê fitas de ph por foto e ordena o histórico com um motor C#"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,  
    allow_methods=["*"],
    allow_headers=["*"],
)

# Descobre a pasta onde o main.py está rodando e aponta para o C# relativo a ela
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
CAMINHO_MOTOR_CSHARP = os.path.join(BASE_DIR, "Motor_C#.exe")

def rgb_para_hex(r, g, b):
    return f"#{r:02x}{g:02x}{b:02x}"

def processar_fita_4_zonas(imagem_bytes):
    nparr = np.frombuffer(imagem_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    img = cv2.resize(img, (400, 600), interpolation=cv2.INTER_AREA)
    
    # === WHITE BALANCE AUTOMÁTICO ===
    # Isso faz o Python "neutralizar" a luz ambiente antes de ler as cores
    result = img.astype(float)
    avg_b = np.average(result[:, :, 0])
    avg_g = np.average(result[:, :, 1])
    avg_r = np.average(result[:, :, 2])
    avg_gray = (avg_b + avg_g + avg_r) / 3

    result[:, :, 0] *= avg_gray / avg_b
    result[:, :, 1] *= avg_gray / avg_g
    result[:, :, 2] *= avg_gray / avg_r
    
    img_wb = np.clip(result, 0, 255).astype(np.uint8)
    
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8,8))

    img_wb[:,:,0] = clahe.apply(img_wb[:,:,0])
    img_wb[:,:,1] = clahe.apply(img_wb[:,:,1])
    img_wb[:,:,2] = clahe.apply(img_wb[:,:,2])
    
    img_rgb = cv2.cvtColor(img_wb, cv2.COLOR_BGR2RGB)
    altura, largura, _ = img_rgb.shape
    altura_quadrado = altura // 4
    cores_extraidas = []

    for i in range(4):
        y_inicial = i * altura_quadrado
        y_final = (i + 1) * altura_quadrado
        margem_esquerda = int(largura * 0.35)
        margem_direita = int(largura * 0.65)
        zona = img_rgb[y_inicial:y_final, margem_esquerda:margem_direita]
        
        # Extrai a cor média já corrigida pela luz
        media_cor = np.median(zona, axis=(0, 1))
        cores_extraidas.append((int(media_cor[0]), int(media_cor[1]), int(media_cor[2])))
    
    cores_extraidas.reverse() 
    return cores_extraidas
    

@app.post("/calcular-ph-real")
async def calcular_ph_real(file: UploadFile = File(...)):
    """Recebe uma foto da fita de PH, processa a cor e ordena usando o C#"""
    try:
        conteudo_imagem = await file.read()

        cores = processar_fita_4_zonas(conteudo_imagem)
        c1, c2, c3, c4 = cores[0], cores[1], cores[2], cores[3]

        # Monta a string de argumentos para o executável C#
        argumento_csharp = f"{c1[0]},{c1[1]},{c1[2]}|{c2[0]},{c2[1]},{c2[2]}|{c3[0]},{c3[1]},{c3[2]}|{c4[0]},{c4[1]},{c4[2]}"
        print(f"CORES ENVIADAS PARA O C#: {argumento_csharp}")
        processo = subprocess.run(
            [CAMINHO_MOTOR_CSHARP, argumento_csharp],
            capture_output=True,
            text=True,
            timeout=5,
        )

        resultado_comando = processo.stdout.strip()
        linhas = resultado_comando.split('\n')

        resultado_final = ""
        for linha in linhas:
            if not linha.startswith("DEBUG") and "|" in linha:
                resultado_final = linha
        if not resultado_final:
            resultado_final = resultado_comando.split('\n')[-1]
        partes = resultado_final.split('|')

        if len(partes) >= 4:
            resultado_ph = partes[0]
            categoria_semantica = partes[1] # <--- NOVO
            lista_ordenada_vinda_do_csharp = partes[2].split(',')
            indice_do_neutro = int(partes[3])
        else:
            # Fallback seguro
            resultado_ph = "7"
            categoria_semantica = "Neutro"
            lista_ordenada_vinda_do_csharp = ["7"]
            indice_do_neutro = 0

        return {
            "status": "Sucesso",
            "ph_detectado": int(float(resultado_ph)),
            "resultado_semantico": categoria_semantica, # <--- ENVIADO PARA O FRONT
            "escala_ordenada": lista_ordenada_vinda_do_csharp,
            "posicao_do_neutro_no_ranking": indice_do_neutro,
            "cores_da_fita_processadas": [
                {"zona": 1, "hex": rgb_para_hex(*c1)},
                {"zona": 2, "hex": rgb_para_hex(*c2)},
                {"zona": 3, "hex": rgb_para_hex(*c3)},
                {"zona": 4, "hex": rgb_para_hex(*c4)}
            ]
        }
    except ValueError as val_err:
        raise HTTPException(status_code=400, detail=str(val_err))
    except subprocess.TimeoutExpired:
        raise HTTPException(status_code=504, detail="O motor C# demorou muito para responder.")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Erro interno no processamento: {str(e)}")