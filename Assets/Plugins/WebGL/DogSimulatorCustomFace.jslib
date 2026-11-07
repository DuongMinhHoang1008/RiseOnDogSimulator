mergeInto(LibraryManager.library, {
  $DogFaceReadImage: async function(file, maxBytes, maxDimension, maxPixels, editorDimension) {
    if (file.size > maxBytes) throw new Error('Image exceeds the 16 MiB limit.');
    if (!/\.(png|jpe?g)$/i.test(file.name) && !/^image\/(png|jpeg)$/i.test(file.type))
      throw new Error('Choose a PNG or JPEG image.');
    var bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    var canvas = document.createElement('canvas');
    try {
      if (bitmap.width > maxDimension || bitmap.height > maxDimension || bitmap.width * bitmap.height > maxPixels)
        throw new Error('Image dimensions exceed the supported limit.');
      var factor = Math.min(1, editorDimension / Math.max(bitmap.width, bitmap.height));
      canvas.width = Math.max(1, Math.round(bitmap.width * factor));
      canvas.height = Math.max(1, Math.round(bitmap.height * factor));
      canvas.getContext('2d', { alpha: true }).drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    } finally { bitmap.close(); }

    var blob = await new Promise(function(resolve) { canvas.toBlob(resolve, 'image/png'); });
    var bytes = new Uint8Array(await blob.arrayBuffer()), binary = '', chunk = 0x8000;
    for (var i = 0; i < bytes.length; i += chunk)
      binary += String.fromCharCode.apply(null, bytes.subarray(i, Math.min(i + chunk, bytes.length)));
    return btoa(binary);
  },

  DogFaceOpenImagePicker__deps: ['$DogFaceReadImage'],
  DogFaceOpenImagePicker: function(objectNamePtr, successPtr, errorPtr, maxBytes, maxDimension, maxPixels, editorDimension) {
    var objectName = UTF8ToString(objectNamePtr);
    var success = UTF8ToString(successPtr);
    var error = UTF8ToString(errorPtr);
    var old = document.getElementById('dog-simulator-face-picker');
    if (old) old.remove();
    var input = document.createElement('input');
    input.id = 'dog-simulator-face-picker';
    input.type = 'file';
    input.accept = 'image/png,image/jpeg';
    input.style.display = 'none';
    document.body.appendChild(input);
    input.addEventListener('cancel', function() { input.remove(); }, { once: true });
    input.addEventListener('change', async function() {
      var file = input.files && input.files[0];
      try {
        if (file) {
          var data = await DogFaceReadImage(file, maxBytes, maxDimension, maxPixels, editorDimension);
          if (input.isConnected) SendMessage(objectName, success, data);
        }
      } catch (e) {
        if (input.isConnected) SendMessage(objectName, error, e.message || 'Image could not be opened.');
      } finally { input.remove(); }
    }, { once: true });
    input.click();
  },

  DogFaceSetImageDrop__deps: ['$DogFaceReadImage'],
  DogFaceSetImageDrop: function(enabled, objectNamePtr, successPtr, errorPtr, maxBytes, maxDimension, maxPixels, editorDimension) {
    var canvas = Module.canvas;
    if (!canvas) return;
    var previous = canvas.dogFaceDrop;
    if (previous) {
      previous.cancelled = true;
      canvas.removeEventListener('dragover', previous.drag);
      canvas.removeEventListener('drop', previous.drop);
      delete canvas.dogFaceDrop;
    }
    if (!enabled) {
      var input = document.getElementById('dog-simulator-face-picker');
      if (input) input.remove();
      return;
    }

    var objectName = UTF8ToString(objectNamePtr);
    var success = UTF8ToString(successPtr);
    var error = UTF8ToString(errorPtr);
    var handlers = {
      cancelled: false,
      drag: function(event) {
        if (event.dataTransfer && Array.from(event.dataTransfer.types).includes('Files')) event.preventDefault();
      },
      drop: async function(event) {
        var file = event.dataTransfer && event.dataTransfer.files[0];
        if (!file) return;
        event.preventDefault();
        try {
          var data = await DogFaceReadImage(file, maxBytes, maxDimension, maxPixels, editorDimension);
          if (!handlers.cancelled) SendMessage(objectName, success, data);
        } catch (e) {
          if (!handlers.cancelled) SendMessage(objectName, error, e.message || 'Image could not be opened.');
        }
      }
    };
    canvas.dogFaceDrop = handlers;
    canvas.addEventListener('dragover', handlers.drag);
    canvas.addEventListener('drop', handlers.drop);
  },

  DogFaceStorageSave: function(jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    var request = indexedDB.open('DogSimulator', 1);
    request.onupgradeneeded = function() { request.result.createObjectStore('customDogFace'); };
    request.onsuccess = function() {
      var tx = request.result.transaction('customDogFace', 'readwrite');
      tx.objectStore('customDogFace').put(json, 'v1');
    };
  },

  DogFaceStorageDelete: function() {
    var request = indexedDB.open('DogSimulator', 1);
    request.onupgradeneeded = function() { request.result.createObjectStore('customDogFace'); };
    request.onsuccess = function() {
      var tx = request.result.transaction('customDogFace', 'readwrite');
      tx.objectStore('customDogFace').delete('v1');
    };
  }
});

