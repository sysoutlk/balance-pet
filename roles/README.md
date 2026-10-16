# roles

角色立绘目录。仓库不附带任何图片（版权原因）。

## 用法

把素材放进本目录，重启桌宠后即可在「设置卡片 -> 外观」里选用。支持：

1. **GIF**：直接放 `.gif`，逐帧播放（用 GIF 自带的帧延迟）。
2. **序列帧目录**：新建一个子目录（名字会显示为角色名），里面放 `1.png 2.png ...`；
   再建 `idle\`、`walk\`、`wave\` 之类的子目录，就能给不同状态配不同帧。
3. **雪碧图**：放一张 PNG，然后在 `pet-config.json` 里写：

   ```json
   "roles": {
     "fps": 8,
     "sheets": {
       "cat.png": {
         "cols": 4, "rows": 5,
         "states": { "idle": [0, 0, 3], "walk": [1, 0, 3], "wave": [2, 0, 3] }
       }
     }
   }
   ```

   每个状态是 `[行号, 起始列, 结束列]`（从 0 开始）。
4. **单张 PNG / JPG / BMP**：静态立绘，只会整体移动与形变。

状态名：`idle`、`walk`、`sleep`，以及动作 `hop bounce spin roll wobble nod shake wave clap
stretch yawn shy startle look greet lie pace peek`。

## 命名

文件名（不含扩展名）就是设置卡片里显示的名字，例如 `鲸鱼娘.png` 会显示为「鲸鱼娘」。

## 说明

- 内置的矢量小鲸鱼不需要图片，`role` 留空即使用它，而且它是带真实动画的（摆尾、眨眼、水花）。
- 使用的图片版权由使用者自行负责。
