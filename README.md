# Valkyria Awakening — Prefab Generation via GitHub Actions

Este projeto gera **prefabs automaticamente na nuvem** — **não precisa instalar Unity localmente**.

---

## Como funciona

1. **Edita dados** em `UnityProject/Assets/Resources/PrefabData/*.json`
2. **Push para GitHub** → GitHub Actions roda Unity 2022.3 LTS na nuvem
3. **Script `PrefabGenerator`** lê JSONs e cria prefabs em `Assets/Prefabs/`
4. **Baixa artefato** `prefabs.zip` na aba Actions → descompacta no projeto local

---

## Estrutura dos JSONs

```json
{
  "prefabs": [
    {
      "name": "NomeDoPrefab",
      "components": [
        {
          "type": "SpriteRenderer",
          "properties": "{\"color\":{\"r\":1,\"g\":1,\"b\":1,\"a\":1}}"
        },
        {
          "type": "SeuScriptCustomizado",
          "properties": "{\"speed\":5,\"health\":100}"
        }
      ]
    }
  ]
}
```

- `type`: Nome da classe (Unity built-in ou seu script)
- `properties`: JSON com campos públicos/serialize do componente

---

## Arquivos de dados incluídos

| Arquivo | Prefabs |
|---------|---------|
| `enemies.json` | 5 inimigos (Skeleton, Goblin, Orc, Slime, Boss Dragon) |
| `weapons.json` | 6 armas (Espadas, Arco, Cajado, Adaga, Machado) |
| `items.json` | 8 itens (Poções, Moedas, Gemas, XP, Chave) |

---

## Passo a passo

### 1. Crie o repositório no GitHub
- Acesse https://github.com/new
- Nome: `valkyria-awakening`
- Público ou Privado
- **Não** inicialize com README/.gitignore

### 2. Push inicial
```bash
cd "C:\Users\leand\OneDrive\Área de Trabalho\Projetos Clauded\valkyria-awakening"
git init
git add .
git commit -m "Initial commit with prefab generation pipeline"
git branch -M main
git remote add origin https://github.com/lkojima/valkyria-awakening.git
git push -u origin main
```

### 3. Configure Unity License (necessário para builds)
1. No GitHub repo: **Settings** → **Secrets and variables** → **Actions** → **New repository secret**
2. Name: `UNITY_LICENSE`
3. Value: Sua licença Unity (obtenha em https://license.unity3d.com/ → "Get License File")
   - Login → Download `.ulf` → abra no Notepad → cole o conteúdo todo

### 4. Rode o workflow
- Vá na aba **Actions** → **Generate Prefabs** → **Run workflow** → **Run workflow**
- Ou faça qualquer push na `main`

### 5. Baixe os prefabs
- Clique no run concluído → **Artifacts** → `prefabs.zip`
- Descompacte em `UnityProject/Assets/Prefabs/`

---

## Editar prefabs depois

1. Modifique `UnityProject/Assets/Resources/PrefabData/*.json`
2. `git add . && git commit -m "Update enemy stats" && git push`
3. Workflow roda automaticamente → novos prefabs no artifact

---

## Scripts necessários no projeto

Crie estes scripts para que os componentes dos JSONs funcionem:

```
UnityProject/Assets/Scripts/
├── EnemyController.cs      // Para inimigos
├── Weapon.cs               // Para armas
├── PickupItem.cs           // Para itens
├── BossAI.cs               // Para boss
├── Projectile_Arrow.cs
├── Projectile_Fireball.cs
```

**Exemplo `EnemyController.cs`:**
```csharp
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public int maxHealth = 30;
    public int damage = 10;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float detectionRange = 8f;
    public int xpValue = 25;
    // ... sua lógica
}
```

---

## Rodar localmente (opcional)

Se instalar Unity depois:
```bash
# No Unity: Tools > Prefab Generator > Generate All Prefabs
# Ou linha de comando:
Unity.exe -batchmode -projectPath "UnityProject" -executeMethod PrefabGenerator.GenerateAll -quit
```

---

## Troubleshooting

| Problema | Solução |
|----------|---------|
| Workflow falha "UNITY_LICENSE" | Adicione secret nas Settings do repo |
| Prefab não criado | Verifique se `type` existe (case-sensitive) |
| Script customizado não encontrado | Coloque scripts em `Assets/Scripts/` (compila antes do Editor) |
| Propriedades não aplicadas | Campos devem ser `public` ou `[SerializeField]` |

---

## Licença

MIT — use livremente.