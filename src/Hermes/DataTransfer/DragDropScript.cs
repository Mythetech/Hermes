// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.DataTransfer;

/// <summary>
/// The page half of native drag and drop, installed at document start in windows that enable it.
/// </summary>
internal static class DragDropScript
{
    public const string Source = """
        (function () {
          // Iframes keep the WebView's own drop handling and never report or reset drops.
          if (window !== window.top || window.__hermesDragDrop) return;
          window.__hermesDragDrop = true;

          var inPageDrag = false;
          var nextSeq = 1;
          var pending = new Map();
          var blazorRegistered = false;

          function hasFiles(dataTransfer) {
            return Array.prototype.indexOf.call(dataTransfer.types, 'Files') >= 0;
          }

          function isEditable(target) {
            return target instanceof HTMLElement
              && (target.isContentEditable || target.closest('input, textarea') !== null);
          }

          // Text dropped on an editable field keeps the browser's own insertion.
          function keepsBrowserDefault(event) {
            return !hasFiles(event.dataTransfer) && isEditable(event.target);
          }

          document.addEventListener('dragstart', function () { inPageDrag = true; }, true);
          document.addEventListener('dragend', function () { inPageDrag = false; }, true);
          // A cancelled dragstart (the drag-out pattern) never fires dragend.
          window.addEventListener('dragstart', function (event) {
            if (event.defaultPrevented) inPageDrag = false;
          });

          function accept(event) {
            if (inPageDrag || !event.dataTransfer || keepsBrowserDefault(event)) return;
            event.preventDefault();
            event.dataTransfer.dropEffect = 'copy';
          }

          window.addEventListener('dragenter', accept, true);
          window.addEventListener('dragover', accept, true);

          window.addEventListener('drop', function (event) {
            if (inPageDrag) {
              inPageDrag = false;
              return;
            }
            if (!event.isTrusted || !event.dataTransfer || keepsBrowserDefault(event)) return;
            event.preventDefault();
            report(event.target, event.clientX, event.clientY, event.dataTransfer);
          }, true);

          function report(target, x, y, dataTransfer) {
            var now = Date.now();
            pending.forEach(function (entry, seq) {
              if (now - entry.time > 10000) pending.delete(seq);
            });

            var seq = nextSeq++;
            pending.set(seq, {
              target: target,
              x: x,
              y: y,
              files: dataTransfer ? Array.prototype.map.call(dataTransfer.files, function (file) {
                return { name: file.name, size: file.size, type: file.type };
              }) : [],
              types: dataTransfer ? Array.prototype.slice.call(dataTransfer.types) : [],
              time: now
            });

            var message = { type: 'hermes-drop', seq: seq, x: x, y: y };
            var webview = window.chrome && window.chrome.webview;
            if (dataTransfer && webview && webview.postMessageWithAdditionalObjects) {
              message.text = dataTransfer.getData('text/plain');
              message.html = dataTransfer.getData('text/html');
              message.uriList = dataTransfer.getData('text/uri-list');
              webview.postMessageWithAdditionalObjects(JSON.stringify(message), dataTransfer.files);
            } else {
              window.external.sendMessage(JSON.stringify(message));
            }
          }

          function registerBlazorEvent() {
            if (blazorRegistered || !window.Blazor || typeof window.Blazor.registerCustomEventType !== 'function') return;
            blazorRegistered = true;
            try {
              window.Blazor.registerCustomEventType('hermesdrop', {
                createEventArgs: function (event) { return event.detail; }
              });
            } catch (error) {
              // The page registered hermesdrop itself.
            }
          }

          window.external.receiveMessage(function (message) {
            if (typeof message !== 'string' || message.indexOf('{"type":"hermes-drop-') !== 0) return;
            var data;
            try {
              data = JSON.parse(message);
            } catch (error) {
              return;
            }

            if (data.type === 'hermes-drop-simulate') {
              report(document.elementFromPoint(data.x, data.y) || document.body, data.x, data.y, null);
              return;
            }

            if (data.type !== 'hermes-drop-ready') return;
            var entry = pending.get(data.seq);
            if (!entry) return;
            pending.delete(data.seq);
            if (!data.dropId) return;

            registerBlazorEvent();
            var target = entry.target && entry.target.isConnected ? entry.target : document;
            target.dispatchEvent(new CustomEvent('hermesdrop', {
              bubbles: true,
              composed: true,
              detail: { dropId: data.dropId, x: entry.x, y: entry.y, files: entry.files, types: entry.types }
            }));
          });

          window.external.sendMessage(JSON.stringify({ type: 'hermes-drop-reset' }));
        })();
        """;
}
