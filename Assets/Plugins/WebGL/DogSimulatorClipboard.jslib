mergeInto(LibraryManager.library, {
  DogSimulator_CopyText: function (textPointer) {
    var text = UTF8ToString(textPointer);

    // execCommand runs synchronously inside the Unity button click. This is the
    // most reliable path when the game is embedded in an itch.io iframe.
    var textArea = document.createElement('textarea');
    textArea.value = text;
    textArea.setAttribute('readonly', '');
    textArea.style.position = 'fixed';
    textArea.style.left = '-9999px';
    textArea.style.top = '0';
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    textArea.setSelectionRange(0, textArea.value.length);

    var copied = false;
    try {
      copied = document.execCommand('copy');
    } catch (error) {
      copied = false;
    }
    document.body.removeChild(textArea);
    if (copied) return 1;

    // Modern browser fallback for hosts that allow clipboard-write in iframes.
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).catch(function (error) {
        console.warn('Could not copy room code to clipboard.', error);
      });
      return 1;
    }

    return 0;
  }
});
