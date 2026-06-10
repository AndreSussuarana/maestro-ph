using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MaestroPH
{
    // Estrutura para representar os canais de cores capturados pelo OpenCV
    public struct Cor 
    { 
        public int R; 
        public int G; 
        public int B; 
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                
                // Validação de segurança: Se o garçom (Python) não mandar os dados, encerra
                if (args.Length == 0) return;

                // 1. RECEBIMENTO E IDENTIFICAÇÃO DAS CORES
                // O argumento chega do Python neste formato: "R,G,B|R,G,B|R,G,B|R,G,B"
                string[] zonas = args[0].Split('|');
                
                Cor c1 = MapearStringParaCor(zonas[0]);
                Cor c2 = MapearStringParaCor(zonas[1]);
                Cor c3 = MapearStringParaCor(zonas[2]);
                Cor c4 = MapearStringParaCor(zonas[3]);

                // 2. CALCULAR O PH ATUAL BASEADO NO GABARITO DA CAIXINHA
                double novoPhDetectado = CalcularPhPelaCaixinha(c1, c2, c3, c4);
                
                string categoria = "";
                if (novoPhDetectado <= 2.5) categoria = "Muito Acido";
                else if (novoPhDetectado <= 5.5) categoria = "Acido";
                else if (novoPhDetectado <= 7.5) categoria = "Neutro";
                else if (novoPhDetectado <= 10.0) categoria = "Basico";
                else categoria = "Extremamente Basico";
                // 3. ATUALIZAÇÃO DO HISTÓRICO DE SIMULAÇÃO
                // Um histórico pré-existente onde vamos injetar a nova leitura feita agora
                List<double> historicoPh = new List<double> { 4.5, 8.2, 5.0, 3.1, 9.5 , 7.0};
                historicoPh.Add(novoPhDetectado); 

                // 4. ALGORITMO DE ORDENAÇÃO: QUICK SORT
                // Organiza o histórico do menor pH para o maior pH
                ExecutarQuickSort(historicoPh, 0, historicoPh.Count - 1);

                // 5. ALGORITMO DE BUSCA: BUSCA BINÁRIA
                // Procura a posição exata do pH neutro (7.0) dentro do histórico já ordenado
                double alvoNeutro = 7.0;
                int posicaoDoNeutro = BuscaBinaria(historicoPh, alvoNeutro);

                // 6. ENTREGA DO RESULTADO PARA O PYTHON (STDOUT)
                // Formatamos a lista separada por vírgulas usando a cultura estável InvariantCulture (usa ponto decimal)
                string listaTexto = string.Join(",", historicoPh.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                
                // O Python vai ler exatamente o que for escrito aqui: "pH_Detectado|Posição_Do_Neutro"
                // Exemplo de saída no console: "7.0|3" ou "5.0|-1" (caso o 7.0 não esteja no histórico)
                string resultadoFinal = $"{novoPhDetectado.ToString(CultureInfo.InvariantCulture)}|{categoria}|{listaTexto}|{posicaoDoNeutro}";
                Console.Write(resultadoFinal);
            }
            catch (Exception ex)
            {
                // Se qualquer erro interno de conversão ou processamento acontecer, o Python recebe o código de falha
                Console.Write($"ERRO|{ex.Message}|-1");
            }
        }

        // --- INTEGRAÇÃO E CONVERSÃO DE DADOS ---

        private static Cor MapearStringParaCor(string blocoCor)
        {
            // Transforma o texto "230,50,50" em uma estrutura Cor prática
            string[] rgb = blocoCor.Split(',');
            return new Cor 
            {
                R = int.Parse(rgb[0]),
                G = int.Parse(rgb[1]),
                B = int.Parse(rgb[2])
            };
        }

        private static double CalcularPhPelaCaixinha(Cor c1, Cor c2, Cor c3, Cor c4)
        {
            // 1. MAPEAMENTO REAL DO GABARITO DA CAIXINHA (Valores de exemplo - ajuste com seus testes)
            // Cada chave é o pH, e o array contém as 4 Cores de cima para baixo daquela coluna
            var gabaritoCaixinha = new Dictionary<int, Cor[]>()
            {
            // Exemplo para pH 0 (Coluna 0 da foto)

            
            
            // ÁCIDOS: Vermelho (R) forte, Azul (B) baixo
            { 0, new Cor[] { new Cor { R=152, G=100, B=78 }, new Cor { R=145, G=126, B=62 }, new Cor { R=155, G=141, B=100 }, new Cor { R=103, G=60, B=114 } }},
            { 1, new Cor[] { new Cor { R=145, G=94, B=68 }, new Cor { R=141, G=121, B=70 }, new Cor { R=147, G=134, B=104 }, new Cor { R=95, G=66, B=108 } }},
            { 2, new Cor[] { new Cor { R=138, G=64, B=50 }, new Cor { R=138, G=98, B=78 }, new Cor { R=139, G=104, B=72 }, new Cor { R=87, G=52, B=76 } }},
            { 3, new Cor[] { new Cor { R=131, G=91, B=91 }, new Cor { R=133, G=116, B=86 }, new Cor { R=132, G=120, B=105 }, new Cor { R=103, G=77, B=102 } }},
            { 4, new Cor[] { new Cor { R=122, G=70, B=72 }, new Cor { R=131, G=111, B=52 }, new Cor { R=124, G=113, B=83 }, new Cor { R=119, G=85, B=72 } }},
            
            { 5, new Cor[] { new Cor { R=123, G=70, B=72 }, new Cor { R=138, G=106, B=57 }, new Cor { R=104, G=103, B=83 }, new Cor { R=149, G=116, B=82 } }},
            { 6, new Cor[] { new Cor { R=114, G=66, B=69 }, new Cor { R=130, G=99, B=57 }, new Cor { R=86, G=94, B=84 }, new Cor { R=136, G=105, B=72 } }},
            { 7, new Cor[] { new Cor { R=117, G=73, B=82 }, new Cor { R=138, G=104, B=63 }, new Cor { R=74, G=91, B=98 }, new Cor { R=143, G=120, B=82 } }},

            { 8, new Cor[] { new Cor { R=133, G=70, B=70 }, new Cor { R=137, G=110, B=70 }, new Cor { R=48, G=85, B=104 }, new Cor { R=135, G=103, B=52 } }},
            { 9, new Cor[] { new Cor { R=111, G=77, B=87 }, new Cor { R=143, G=104, B=81 }, new Cor { R=66, G=94, B=117 }, new Cor { R=149, G=108, B=68 } }},
            { 10, new Cor[] { new Cor { R=105, G=73, B=79 }, new Cor { R=119, G=85, B=82 }, new Cor { R=66, G=93, B=102 }, new Cor { R=161, G=120, B=73 } }},
            { 11, new Cor[] { new Cor { R=104, G=68, B=70 }, new Cor { R=95, G=66, B=84 }, new Cor { R=65, G=93, B=107 }, new Cor { R=173, G=132, B=77 } }},
            { 12, new Cor[] { new Cor { R=80, G=54, B=66 }, new Cor { R=93, G=62, B=83 }, new Cor { R=63, G=89, B=101 }, new Cor { R=172, G=126, B=68 } }},
            { 13, new Cor[] { new Cor { R=73, G=59, B=69 }, new Cor { R=93, G=63, B=82 }, new Cor { R=64, G=88, B=100 }, new Cor { R=176, G=122, B=65 } }},
            { 14, new Cor[] { new Cor { R=59, G=53, B=66 }, new Cor { R=94, G=68, B=80 }, new Cor { R=54, G=80, B=92 }, new Cor { R=144, G=83, B=27 } }},
            };
            
                    // ... Complete aqui do pH 0 ao 14 conforme os quadradinhos da foto
                

            int melhorPhMatch = 6; // pH padrão de segurança caso falte dados
            double menorDistanciaTotal = double.MaxValue;

        
            // 2. ALGORITMO DA DISTÂNCIA EUCLIDIANA COMBINADA (As 4 zonas juntas)
            foreach (var par in gabaritoCaixinha)
            {
                int phFoco = par.Key;
                Cor[] coresG = par.Value;

                // Seus pesos dinâmicos
                double avgR = (c1.R + c2.R + c3.R + c4.R) / 4.0;
                double avgG = (c1.G + c2.G + c3.G + c4.G) / 4.0;
                double avgB = (c1.B + c2.B + c3.B + c4.B) / 4.0;

                double pesoR = (avgR > avgG && avgR > avgB) ? 12.0 : 1.0;
                double pesoB = (avgB > avgR && avgB > avgG) ? 12.0 : 1.0;
                double pesoG = (avgG > avgR && avgG > avgB) ? 12.0 : 1.0;

                // Cálculo das distâncias (d1 com pesos, d2-d4 base)
                double d1 = (Math.Pow(c1.R - coresG[0].R, 2) * pesoR) + 
                            (Math.Pow(c1.G - coresG[0].G, 2) * pesoG) + 
                            (Math.Pow(c1.B - coresG[0].B, 2) * pesoB);

                double d2 = (Math.Pow(c2.R - coresG[1].R, 2) * pesoR) + (Math.Pow(c2.G - coresG[1].G, 2) * pesoG) + (Math.Pow(c2.B - coresG[1].B, 2) * pesoB);
                double d3 = (Math.Pow(c3.R - coresG[2].R, 2) * pesoR) + (Math.Pow(c3.G - coresG[2].G, 2) * pesoG) + (Math.Pow(c3.B - coresG[2].B, 2) * pesoB);
                double d4 = (Math.Pow(c4.R - coresG[3].R, 2) * pesoR) + (Math.Pow(c4.G - coresG[3].G, 2) * pesoG) + (Math.Pow(c4.B - coresG[3].B, 2) * pesoB);
    
                double distanciaTotal = Math.Sqrt(d1 + d2 + d3 + d4);

                // --- CORREÇÃO: Penalidade cirúrgica ---
                // Só penaliza o pH 7 se o vermelho da fita (c1.R) for alto, 
                // indicando que a amostra provavelmente não é neutra.
                
                // Comparação de vencedor
                if (distanciaTotal < menorDistanciaTotal)
                {
                    menorDistanciaTotal = distanciaTotal;
                    melhorPhMatch = phFoco;
                }
            }
            return melhorPhMatch;
        }
        // --- ALGORITMO DE ESTRUTURA DE DADOS: QUICK SORT ---

        private static void ExecutarQuickSort(List<double> lista, int baixo, int alto)
        {
            if (baixo < alto)
            {
                int indiceParticao = Particionar(lista, baixo, alto);

                // Ordena as metades separadamente
                ExecutarQuickSort(lista, baixo, indiceParticao - 1);
                ExecutarQuickSort(lista, indiceParticao + 1, alto);
            }
        }

        private static int Particionar(List<double> lista, int baixo, int alto)
        {
            double pivo = lista[alto];
            int i = (baixo - 1);

            for (int j = baixo; j < alto; j++)
            {
                // Se o elemento atual for menor ou igual ao pivô
                if (lista[j] <= pivo)
                {
                    i++;
                    // Troca os elementos de lugar
                    double temp = lista[i];
                    lista[i] = lista[j];
                    lista[j] = temp;
                }
            }

            // Troca o pivô com o elemento da posição correta
            double temp2 = lista[i + 1];
            lista[i + 1] = lista[alto];
            lista[alto] = temp2;

            return i + 1;
        }

        // --- ALGORITMO DE ESTRUTURA DE DADOS: BUSCA BINÁRIA ---

        private static int BuscaBinaria(List<double> lista, double alvo)
        {
            int esquerdo = 0;
            int direito = lista.Count - 1;

            while (esquerdo <= direito)
            {
                int meio = esquerdo + (direito - esquerdo) / 2;

                // Usamos Math.Abs para comparar doubles devido a aproximações de ponto flutuante
                if (Math.Abs(lista[meio] - alvo) < 0.01)
                {
                    return meio; // Alvo encontrado! Retorna o índice no ranking
                }

                if (lista[meio] < alvo)
                {
                    esquerdo = meio + 1;
                }
                else
                {
                    direito = meio - 1;
                }
            }

            return -1; // Caso o valor 7.0 não exista na lista atualizada
        }
    }
}