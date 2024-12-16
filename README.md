# UnityGif

GIF player for Unity Engine (Based on [UniGif](https://github.com/westhillapps/UniGif) decoding)

适用于 Unity 引擎的 GIF 播放器（基于 [UniGif](https://github.com/westhillapps/UniGif) 解码）

**<font size = 4>[English document](#English)</font>**

**<font size = 4>[中文文档](#Chinese)</font>**

## Instructions for Use<a id = "English"></a>

### Features

* Support `GIF87a` or `GIF89a` format
* Unified management of GIF files
* Make GIF play with a simple API call
* Support multi-threaded, multi-coroutine play GIF
* Lightweight player, import and use, high portability

### Preparation

* Import the `UnityGif` folder anywhere in the project's `Assets` folder
* Add the `.bytes` suffix to all GIF file extensions (e.g. Change `example.gif` to `example.gif.bytes` and batch all GIF files in Windows using the batch command `ren *.gif *.gif.bytes` ).
* **Note: GIF file name cannot be repeated!!!**

### How to use

1. Import all processed GIF files into Unity and the script will automatically process these GIF files, turning them into globally unique `.asset` files
2. Load the GIF file (Resources is an example here) and prepare a RawImage

    ``` CSharp
    GifData gifData = Resources.Load<GifData>("example.gif");
    RawImage rawImage = GameObject.Find("ExampleGameObject").GetComponent<RawImage>();
    ```

3. Use the following API to manipulate GIF (which needs to be called after Unity script initialization, or simply after Awake life cycle events (including)) :
   1. `GifImage.Instance.Play(gifData, rawImage)` : Plays gifData on the specified rawImage
   2. `GifImage.Instance.Pause()` : suspends all gifData being played
   3. `GifImage.Instance.Pause(gifData, rawImage)` : pause gifData played on rawImage
   4. `GifImage.Instance.Stop()` : stops all gifData being played
   5. `GifImage.Instance.Stop(gifData, rawImage)` : stops gifData playing on rawImage
   6. `GifImage.Instance.Clear()` : clears all gifData data
   7. `GifImage.Instance.Clear(gifData)` : clears the specified gifData data
   * In order to save memory space, GIF is decoded only when it is played for the first time (making textures), the process is asynchronous, so it will take a while to play it for the first time. According to ` GifImage. Instance. GifState [gifData. Name] [rawImage. GetHashCode ()] ` access to current state of the object to handle:
   1. `None` : GIF is not initialized
   2. `Loading` : GIF is loading the texture
   3. `Ready` : The GIF texture has been loaded
   4. `Playing` : GIF is playing
   5. `Pause` : The GIF is paused
   * The `clear()` function is used to destroy the texture data of the GIF, and the GIF will be decoded again the next time it is played.
4. If you need to change the filter mode or wrap mode of the GIF texture (which has not been decoded) while playing, directly on the ` GifImage.Instance.FilterMode ` or ` GifImage.Instance.WrapMode ` assignment. If the GIF has already been decoded, it needs to be re-decoded (use 'Clear()' to clear the old data and then use 'Play()' to re-decode). **Note: Texture mode acts on global decoding!!!**

### To-do list

* Parts that may be optimized in the future:
  1. Reuse textures using object pools
  2. Batch play of multiple coroutine GIFs (render in batches)
* Features may be added in the future:
  1. Get the GIF online and play it

## 使用说明<a id = "Chinese"></a>

### 特点

* 支持 `GIF87a` 或 `GIF89a` 格式
* 对 GIF 文件进行统一管理
* 通过简单的 API 调用使 GIF 播放
* 支持多线程、多个协程播放 GIF
* 轻量级播放器，导入即用，可移植性高

### 准备工作

* 将 `UnityGif` 文件夹导入项目的 `Assets` 文件夹中的任意位置
* 在所有 GIF 文件的后缀名后添加 `.bytes` 后缀名（例如：`example.gif` 更改为 `example.gif.bytes` ，在 Windows 系统中可使用批处理命令 `ren *.gif *.gif.bytes` 批处理所有 GIF 文件）。
* **注意：GIF 的文件名不可重复！！！**

### 如何使用

1. 将所有处理过的 GIF 文件导入 Unity 中，脚本将自动处理这些 GIF 文件，将它们转变为全局唯一的 `.asset` 文件
2. 加载 GIF 文件（这里以 Resources 举例）并准备一个 RawImage

    ``` CSharp
    GifData gifData = Resources.Load<GifData>("example.gif");
    RawImage rawImage = GameObject.Find("ExampleGameObject").GetComponent<RawImage>();
    ```

3. 使用以下 API 操作 GIF（需要在 Unity 脚本初始化后调用，简单来说要在 Awake 生命周期事件（包括）之后调用）：
    1. `GifImage.Instance.Play(gifData, rawImage)`：在指定的 rawImage 上播放 gifData
    2. `GifImage.Instance.Pause()`：暂停所有正在播放的 gifData
    3. `GifImage.Instance.Pause(gifData, rawImage)`：暂停在 rawImage 上播放的 gifData
    4. `GifImage.Instance.Stop()`：停止所有正在播放的 gifData
    5. `GifImage.Instance.Stop(gifData, rawImage)`：停止在 rawImage 上播放的 gifData
    6. `GifImage.Instance.Clear()`：清除所有的 gifData 数据
    7. `GifImage.Instance.Clear(gifData)`：清除指定的 gifData 数据
    * 为节约内存空间，GIF 只有在第一次播放的时候才会进行解码（制作纹理），该过程是异步的，所以第一次播放的时候需要等待一段时间才会播放，可以根据 `GifImage.Instance.gifState[gifData.name][rawImage.GetHashCode()]` 获取当前对象的状态自行处理：
        1. `None`：GIF 未初始化
        2. `Loading`：GIF 正在加载纹理
        3. `Ready`：GIF 纹理已加载完毕
        4. `Playing`：GIF 正在播放
        5. `Pause`：GIF 已暂停
    * `clear()` 函数则是用来销毁 GIF 的纹理数据，使用后下一次播放该 GIF 会再次进行解码（制作纹理）。
4. 如果需要更改播放时 GIF 纹理（未解码过）的过滤模式或包裹模式，直接对 `GifImage.Instance.FilterMode` 或 `GifImage.Instance.WrapMode` 赋值即可。如果 GIF 已经解码过了，则需要重新解码（使用 `Clear()` 函数清除旧数据后再使用 `Play()` 函数重新解码）。**注意：纹理模式作用于全局解码！！！**

### 待办事项

* 未来可能会优化的部分：
  1. 使用对象池对纹理进行复用
  2. 批处理多个协程 GIF 的播放（渲染合批）
* 未来可能会添加的功能：
  1. 在线获取 GIF 并播放
