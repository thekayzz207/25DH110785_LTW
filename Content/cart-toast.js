"use strict";
(function () {
  if (
    typeof window.fetch !== "function" ||
    typeof window.FormData !== "function" ||
    typeof window.URL !== "function" ||
    typeof window.Promise !== "function"
  ) {
    return;
  }

  var addQueue = window.Promise.resolve();
  var toastRegion = null;
  var activeToasts = [];
  var uncertainMessage =
    "Chưa xác nhận được kết quả thêm sản phẩm. Vui lòng kiểm tra giỏ hàng trước khi thử lại.";

  function ensureToastRegion() {
    if (toastRegion && document.documentElement.contains(toastRegion)) {
      return toastRegion;
    }
    toastRegion = document.createElement("div");
    toastRegion.className = "cart-toast-region";
    toastRegion.setAttribute("aria-live", "polite");
    toastRegion.setAttribute("aria-relevant", "additions text");
    toastRegion.setAttribute("aria-atomic", "false");
    document.body.appendChild(toastRegion);
    return toastRegion;
  }

  function removeToast(entry) {
    window.clearTimeout(entry.timer);
    var index = activeToasts.indexOf(entry);
    if (index !== -1) activeToasts.splice(index, 1);
    if (entry.element.parentNode) {
      entry.element.parentNode.removeChild(entry.element);
    }
  }

  function showToast(kind, message, title) {
    var region = ensureToastRegion();
    var toast = document.createElement("section");
    toast.className = "cart-toast cart-toast--" + kind;
    toast.setAttribute("role", kind === "error" ? "alert" : "status");
    toast.setAttribute("aria-live", kind === "error" ? "assertive" : "polite");
    toast.setAttribute("aria-atomic", "true");

    var icon = document.createElement("span");
    icon.className = "cart-toast__icon";
    icon.setAttribute("aria-hidden", "true");
    icon.textContent = kind === "error" ? "!" : "✓";

    var content = document.createElement("div");
    content.className = "cart-toast__content";
    var heading = document.createElement("strong");
    heading.className = "cart-toast__title";
    heading.textContent = title;
    var description = document.createElement("p");
    description.className = "cart-toast__message";
    description.textContent = message;
    content.appendChild(heading);
    content.appendChild(description);

    var close = document.createElement("button");
    close.type = "button";
    close.className = "cart-toast__close";
    close.setAttribute("aria-label", "Đóng thông báo giỏ hàng");
    close.textContent = "×";

    toast.appendChild(icon);
    toast.appendChild(content);
    toast.appendChild(close);
    var entry = { element: toast, timer: null };
    close.addEventListener("click", function () {
      removeToast(entry);
    });
    activeToasts.push(entry);
    region.insertBefore(toast, region.firstChild);
    while (activeToasts.length > 3) {
      removeToast(activeToasts[0]);
    }
    entry.timer = window.setTimeout(
      function () {
        removeToast(entry);
      },
      kind === "error" ? 6000 : 4000,
    );
  }

  function validCartCount(value) {
    return (
      typeof value === "number" &&
      isFinite(value) &&
      Math.floor(value) === value &&
      value >= 0 &&
      value <= 2147483647
    );
  }

  function validPayload(data) {
    return (
      data !== null &&
      typeof data === "object" &&
      !Array.isArray(data) &&
      typeof data.success === "boolean" &&
      typeof data.message === "string" &&
      validCartCount(data.cartCount)
    );
  }

  function updateCartCount(count) {
    if (!validCartCount(count)) return;
    var badges = document.querySelectorAll(".cart-link b, [data-cart-count]");
    Array.prototype.forEach.call(badges, function (badge) {
      badge.textContent = String(count);
    });
  }

  function restoreSubmission(submission) {
    Array.prototype.forEach.call(submission.buttons, function (state) {
      state.element.disabled = state.disabled;
    });
    submission.form.removeAttribute("data-cart-toast-pending");
    document.removeEventListener("focusin", submission.trackFocus);

    // A disabled submit button can lose focus. Do not steal focus if the user
    // has moved to another control while this submission was queued/running.
    var focused = document.activeElement;
    if (
      submission.restoreFocus &&
      !submission.focusMoved &&
      (focused === document.body || focused === document.documentElement) &&
      document.documentElement.contains(submission.initialFocus) &&
      !submission.initialFocus.disabled
    ) {
      try {
        submission.initialFocus.focus({ preventScroll: true });
      } catch (ignore) {
        // Never fall back to focus() without preventScroll.
      }
    }
  }

  function responseError(response, data) {
    if (validPayload(data) && data.success === false && data.message.trim()) {
      return data.message;
    }
    if (response.status === 400 || response.status === 403) {
      return "Phiên thao tác chưa hợp lệ. Vui lòng kiểm tra giỏ hàng, rồi tải lại trang trước khi thử lại.";
    }
    return uncertainMessage;
  }

  function sendAdd(submission) {
    return window.Promise.resolve()
      .then(function () {
        return window.fetch(submission.url, {
          method: "POST",
          credentials: "same-origin",
          mode: "same-origin",
          redirect: "error",
          headers: {
            "X-Requested-With": "XMLHttpRequest",
            Accept: "application/json",
          },
          body: submission.body,
        });
      })
      .then(function (response) {
        return response.text().then(function (body) {
          var data = null;
          try {
            data = JSON.parse(body);
          } catch (ignore) {
            // Login/CSRF/error HTML is an uncertain result, never a success.
          }
          if (!response.ok) {
            showToast(
              "error",
              responseError(response, data),
              "Cần kiểm tra giỏ hàng",
            );
            return;
          }
          if (!validPayload(data)) {
            showToast("error", uncertainMessage, "Cần kiểm tra giỏ hàng");
            return;
          }
          updateCartCount(data.cartCount);
          if (data.success) {
            if (submission.isReorder) {
              var cartLink = submission.form.querySelector("[data-reorder-cart-link]");
              var button = submission.form.querySelector("button[type='submit']");
              if (button && cartLink) {
                button.hidden = true;
                cartLink.hidden = false;
                submission.form.setAttribute("data-reorder-added", "true");
                if (submission.restoreFocus && !submission.focusMoved) {
                  cartLink.focus({ preventScroll: true });
                }
              }
            }
            showToast(
              "success",
              data.message.trim() || "Đã thêm sản phẩm vào giỏ hàng.",
              "Đã thêm vào giỏ hàng",
            );
          } else {
            showToast(
              "error",
              data.message.trim() ||
                "Chưa thể thêm sản phẩm. Vui lòng kiểm tra giỏ hàng và thử lại.",
              submission.isReorder ? "Chưa thể mua lại cả đơn" : "Chưa thể thêm sản phẩm",
            );
          }
        });
      })
      .catch(function () {
        showToast("error", uncertainMessage, "Cần kiểm tra giỏ hàng");
      })
      .then(
        function () {
          restoreSubmission(submission);
        },
        function () {
          restoreSubmission(submission);
        },
      );
  }

  function submitControls(form) {
    return Array.prototype.filter.call(form.elements, function (element) {
      return (
        element.form === form &&
        (element.type === "submit" || element.type === "image")
      );
    });
  }

  document.addEventListener("submit", function (event) {
    if (event.defaultPrevented) return;
    var form = event.target;
    if (!form || form.tagName !== "FORM") return;
    var isReorder = form.classList.contains("reorder-form");
    if (!isReorder && !form.classList.contains("add-form") && !form.classList.contains("detail-buy")) {
      return;
    }

    var submitter = event.submitter;
    var overrideMethod = submitter && submitter.getAttribute("formmethod");
    var method = (overrideMethod || form.method || "GET").toUpperCase();
    if (method !== "POST") return;
    var overrideAction = submitter && submitter.getAttribute("formaction");
    var target;
    try {
      target = new window.URL(overrideAction || form.action, document.baseURI);
    } catch (ignore) {
      return;
    }
    var expectedPath = isReorder ? /\/order\/reorder(?:\/\d+)?\/?$/i : /\/cart\/add\/?$/i;
    if (target.origin !== window.location.origin || !expectedPath.test(target.pathname)) {
      return;
    }

    if (form.getAttribute("data-cart-toast-pending") === "true" || form.hasAttribute("data-reorder-added")) {
      event.preventDefault();
      return;
    }
    if (typeof form.checkValidity === "function" && !form.checkValidity()) {
      event.preventDefault();
      if (typeof form.reportValidity === "function") form.reportValidity();
      return;
    }

    var body;
    try {
      // Snapshot values, including the anti-forgery token, before disabling
      // buttons or waiting behind another form's add request.
      body = new window.FormData(form);
      if (
        submitter &&
        submitter.form === form &&
        submitter.name &&
        !submitter.disabled
      ) {
        body.append(submitter.name, submitter.value);
      }
    } catch (ignore) {
      // No request has started, so the browser's normal form POST is safe.
      return;
    }

    event.preventDefault();
    var focused = document.activeElement;
    var buttons = submitControls(form).map(function (button) {
      return { element: button, disabled: button.disabled };
    });
    var submission = {
      form: form,
      isReorder: isReorder,
      url: target.href,
      body: body,
      buttons: buttons,
      initialFocus: focused,
      restoreFocus: buttons.some(function (state) {
        return state.element === focused;
      }),
      focusMoved: false,
      trackFocus: null,
    };
    submission.trackFocus = function (focusEvent) {
      if (focusEvent.target !== submission.initialFocus) {
        submission.focusMoved = true;
      }
    };
    document.addEventListener("focusin", submission.trackFocus);
    form.setAttribute("data-cart-toast-pending", "true");
    buttons.forEach(function (state) {
      state.element.disabled = true;
    });

    // Serialize adds across all forms so later server counts cannot be
    // overwritten by an older response arriving out of order.
    var run = function () {
      return sendAdd(submission);
    };
    addQueue = addQueue.then(run, run);
  });
})();
