"use strict";
(function () {
  if (
    typeof window.fetch !== "function" ||
    typeof window.FormData !== "function" ||
    typeof window.URL !== "function" ||
    typeof window.Promise !== "function"
  )
    return;

  function initialize() {
    var root = document.querySelector("[data-cart-quantity]");
    if (!root || root.getAttribute("data-cart-quantity-ready") === "true")
      return;
    var elements = root.querySelectorAll("[data-cart-quantity-row]");
    if (!elements.length) return;

    var state = {
      rows: [],
      running: null,
      timer: null,
      sequence: 0,
      frozen: false,
      externalChange: false,
      notice: "",
      noticeKind: "",
      snapshot: null,
    };
    var status = root.querySelector("[data-cart-quantity-status]");
    var recheck = root.querySelector("[data-cart-quantity-recheck]");
    var filled = root.querySelector("[data-cart-quantity-filled]");
    var empty = root.querySelector("[data-cart-quantity-empty]");
    var checkoutLinks = Array.prototype.map.call(
      root.querySelectorAll("[data-cart-quantity-checkout]"),
      function (link) {
        return {
          element: link,
          href: link.getAttribute("href"),
          role: link.getAttribute("role"),
          tabIndex: link.getAttribute("tabindex"),
        };
      },
    );
    if (!status || !recheck || !filled || !empty) return;

    function safeInteger(value, minimum, maximum) {
      return (
        typeof value === "number" &&
        isFinite(value) &&
        Math.floor(value) === value &&
        value >= minimum &&
        value <= (maximum === undefined ? 9007199254740991 : maximum)
      );
    }
    function attributeInteger(element, name, minimum, maximum) {
      var raw = element.getAttribute(name);
      if (raw === null || !/^\d+$/.test(raw)) return null;
      var value = Number(raw);
      return safeInteger(value, minimum, maximum) ? value : null;
    }
    function ownPostAction(form, controllerAction) {
      if (
        !form ||
        (form.getAttribute("method") || "get").toUpperCase() !== "POST"
      )
        return null;
      try {
        var url = new window.URL(
          form.getAttribute("action") || window.location.href,
          document.baseURI,
        );
        return url.origin === window.location.origin &&
          controllerAction.test(url.pathname)
          ? url.href
          : null;
      } catch (ignore) {
        return null;
      }
    }
    var validInitial = true;
    Array.prototype.forEach.call(elements, function (element) {
      var form = element.querySelector("[data-cart-quantity-form]");
      var input = element.querySelector("[data-cart-quantity-input]");
      var lineTotal = element.querySelector("[data-cart-quantity-line-total]");
      var rowMessage = element.querySelector(
        "[data-cart-quantity-row-message]",
      );
      var id = attributeInteger(element, "data-product-id", 1, 2147483647);
      var price = attributeInteger(element, "data-unit-price-cents", 0);
      var quantity = attributeInteger(
        element,
        "data-confirmed-quantity",
        1,
        99,
      );
      var stock = attributeInteger(
        element,
        "data-stock-quantity",
        0,
        2147483647,
      );
      var maximum = attributeInteger(element, "data-max-quantity", 0, 99);
      var available = element.getAttribute("data-available");
      var url = ownPostAction(form, /\/cart\/update\/?$/i);
      if (
        !input ||
        !lineTotal ||
        !rowMessage ||
        id === null ||
        price === null ||
        quantity === null ||
        stock === null ||
        maximum === null ||
        !url ||
        (available !== "true" && available !== "false") ||
        !safeInteger(price * quantity, 0) ||
        state.rows.some(function (r) {
          return r.id === id;
        })
      ) {
        validInitial = false;
        return;
      }
      state.rows.push({
        id: id,
        element: element,
        form: form,
        input: input,
        url: url,
        lineTotal: lineTotal,
        rowMessage: rowMessage,
        unitPrice: price,
        confirmedQty: quantity,
        desiredQty: quantity,
        stock: stock,
        maximum: maximum,
        available: available === "true",
        version: 0,
        sequence: 0,
        editedAt: 0,
        dirty: false,
        invalid: false,
        rawInvalid: false,
        lastInput: input.value,
        lineTotalText: lineTotal.textContent.trim(),
        removeButtons: Array.prototype.map.call(
          element.querySelectorAll(
            "[data-cart-quantity-remove] button[type='submit'], [data-cart-quantity-remove] button:not([type]), [data-cart-quantity-remove] input[type='submit']",
          ),
          function (button) {
            return { element: button, disabled: button.disabled };
          },
        ),
      });
    });
    if (!validInitial || state.rows.length !== elements.length) return;

    var initialTotal = state.rows.reduce(function (sum, row) {
      return sum + row.unitPrice * row.confirmedQty;
    }, 0);
    if (!safeInteger(initialTotal, 0)) return;
    var initialTotalElement = root.querySelector("[data-cart-quantity-total]");
    state.snapshot = {
      totalCents: initialTotal,
      totalText: initialTotalElement
        ? initialTotalElement.textContent.trim()
        : money(initialTotal),
      cartCount: state.rows.reduce(function (sum, row) {
        return sum + row.confirmedQty;
      }, 0),
    };

    function money(cents) {
      // All arithmetic stays in integer cents; only display uses the VND locale.
      if (window.Intl && window.Intl.NumberFormat) {
        return (
          new window.Intl.NumberFormat("vi-VN", {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2,
          }).format(cents / 100) + " ₫"
        );
      }
      var whole = String(Math.floor(cents / 100)).replace(
        /\B(?=(\d{3})+(?!\d))/g,
        ".",
      );
      var fraction = cents % 100;
      return (
        whole +
        (fraction
          ? "," +
            (fraction < 10 ? "0" : "") +
            String(fraction).replace(/0$/, "")
          : "") +
        " ₫"
      );
    }
    function findRow(element) {
      var rowElement = element.closest("[data-cart-quantity-row]");
      if (!rowElement || !root.contains(rowElement)) return null;
      return (
        state.rows.filter(function (row) {
          return row.element === rowElement;
        })[0] || null
      );
    }
    function hasQueuedWork() {
      return state.rows.some(function (row) {
        return row.dirty && !row.invalid && row.available;
      });
    }
    function busy() {
      return !!state.running || hasQueuedWork();
    }
    function checkoutBlocked() {
      return (
        state.frozen ||
        busy() ||
        !state.rows.length ||
        state.rows.some(function (row) {
          return row.dirty || row.invalid || !row.available;
        })
      );
    }
    function validateRow(row) {
      row.invalid =
        row.rawInvalid || !row.available || row.desiredQty > row.maximum;
      row.dirty = row.desiredQty !== row.confirmedQty;
      var message = "";
      if (!row.available)
        message =
          "Sản phẩm tạm hết hàng hoặc ngừng bán. Bạn có thể xóa khỏi giỏ.";
      else if (row.rawInvalid)
        message = "Nhập số lượng nguyên từ 1 đến " + row.maximum + ".";
      else if (row.desiredQty > row.maximum)
        message =
          "Hiện chỉ còn mua tối đa " +
          row.maximum +
          " sản phẩm. Hãy giảm số lượng hoặc xóa khỏi giỏ.";
      row.rowMessage.textContent = message;
      row.rowMessage.hidden = !message;
      row.input.setAttribute("aria-invalid", row.invalid ? "true" : "false");
      row.element.classList.toggle("cart-quantity-invalid", row.invalid);
    }
    function setText(selector, value) {
      Array.prototype.forEach.call(
        root.querySelectorAll(selector),
        function (element) {
          element.textContent = value;
        },
      );
    }
    function updateHeader(count) {
      if (!safeInteger(count, 0, 2147483647)) return;
      Array.prototype.forEach.call(
        document.querySelectorAll(".cart-link b, [data-cart-count]"),
        function (badge) {
          badge.textContent = String(count);
        },
      );
    }
    function render() {
      var localTotal = 0,
        localCount = 0,
        localDifference = false;
      state.rows.forEach(function (row) {
        validateRow(row);
        var displayQty = row.rawInvalid ? row.confirmedQty : row.desiredQty;
        var lineCents = row.unitPrice * displayQty;
        localTotal += lineCents;
        localCount += displayQty;
        if (displayQty !== row.confirmedQty) localDifference = true;
        row.lineTotal.textContent =
          displayQty === row.confirmedQty && row.lineTotalText
            ? row.lineTotalText
            : money(lineCents);
        row.input.max = String(row.maximum);
        row.input.disabled = state.frozen || !row.available;
        row.element.classList.toggle(
          "cart-quantity-is-pending",
          !!state.running && state.running.row === row,
        );
        Array.prototype.forEach.call(
          row.element.querySelectorAll("[data-cart-quantity-step]"),
          function (button) {
            var step = Number(button.getAttribute("data-cart-quantity-step"));
            button.disabled =
              state.frozen ||
              !row.available ||
              (step < 0 ? displayQty <= 1 : displayQty >= row.maximum);
          },
        );
        var stockText = row.element.querySelector("[data-cart-quantity-stock]");
        if (stockText)
          stockText.textContent = row.available
            ? "Còn " + row.stock + " sản phẩm"
            : "Tạm hết hàng hoặc ngừng bán";
        var unitText = row.element.querySelector(
          "[data-cart-quantity-unit-price]",
        );
        if (unitText) unitText.textContent = money(row.unitPrice);
        row.removeButtons.forEach(function (button) {
          button.element.disabled = button.disabled || state.frozen || busy();
        });
      });
      var totalText =
        !localDifference || state.externalChange
          ? state.snapshot.totalText
          : money(localTotal);
      setText(
        "[data-cart-quantity-subtotal], [data-cart-quantity-total]",
        totalText,
      );
      setText(
        "[data-cart-quantity-count]",
        String(state.externalChange ? state.snapshot.cartCount : localCount),
      );
      filled.hidden = !state.rows.length && !state.externalChange;
      empty.hidden = !!state.rows.length || state.externalChange;
      var blocked = checkoutBlocked();
      checkoutLinks.forEach(function (saved) {
        var link = saved.element;
        link.setAttribute("aria-disabled", blocked ? "true" : "false");
        link.classList.toggle("cart-quantity-blocked", blocked);
        if (blocked) {
          // Removing href also prevents middle-click / open-in-new-tab from
          // bypassing the pending-write guard. No navigation is triggered.
          link.removeAttribute("href");
          link.setAttribute("role", "link");
          link.setAttribute("tabindex", "0");
        } else {
          if (saved.href === null) link.removeAttribute("href");
          else link.setAttribute("href", saved.href);
          if (saved.role === null) link.removeAttribute("role");
          else link.setAttribute("role", saved.role);
          if (saved.tabIndex === null) link.removeAttribute("tabindex");
          else link.setAttribute("tabindex", saved.tabIndex);
        }
      });
      root.setAttribute(
        "aria-busy",
        busy() && !state.frozen ? "true" : "false",
      );
      recheck.hidden = !state.frozen;
      var invalid = state.rows.some(function (row) {
        return row.invalid;
      });
      var message = state.notice;
      var isError = state.noticeKind === "error" || state.frozen;
      if (!state.frozen && invalid) {
        message =
          "Vui lòng sửa số lượng chưa hợp lệ hoặc xóa sản phẩm hết hàng trước khi đặt mua.";
        isError = true;
      } else if (!state.frozen && busy()) {
        message =
          state.noticeKind === "error" && message
            ? message + " Đang cập nhật các thay đổi còn lại…"
            : "Đang cập nhật giỏ hàng…";
      } else if (!message) {
        message = "Đổi số lượng để tự động cập nhật giỏ hàng.";
      }
      status.textContent = message;
      status.setAttribute("role", isError ? "alert" : "status");
      status.setAttribute("aria-live", isError ? "assertive" : "polite");
      status.classList.toggle("cart-quantity-status-error", isError);
    }
    function freeze(message) {
      state.frozen = true;
      state.notice = message;
      state.noticeKind = "error";
      window.clearTimeout(state.timer);
      state.timer = null;
      state.rows.forEach(function (row) {
        row.desiredQty = row.confirmedQty;
        row.rawInvalid = false;
        row.dirty = false;
        row.input.value = String(row.confirmedQty);
        row.lastInput = row.input.value;
      });
      render();
    }
    function validSnapshot(data) {
      if (
        !data ||
        typeof data !== "object" ||
        typeof data.success !== "boolean" ||
        typeof data.message !== "string" ||
        !safeInteger(data.cartCount, 0, 2147483647) ||
        !safeInteger(data.totalCents, 0) ||
        typeof data.totalText !== "string" ||
        !Array.isArray(data.items)
      )
        return false;
      var ids = Object.create(null),
        total = 0,
        count = 0;
      for (var i = 0; i < data.items.length; i++) {
        var item = data.items[i];
        if (
          !item ||
          !safeInteger(item.productId, 1, 2147483647) ||
          ids[item.productId] ||
          !safeInteger(item.quantity, 1, 99) ||
          !safeInteger(item.stockQuantity, 0, 2147483647) ||
          !safeInteger(item.maxQuantity, 0, 99) ||
          !safeInteger(item.unitPriceCents, 0) ||
          !safeInteger(item.lineTotalCents, 0) ||
          typeof item.lineTotalText !== "string" ||
          typeof item.available !== "boolean" ||
          !safeInteger(item.unitPriceCents * item.quantity, 0) ||
          item.unitPriceCents * item.quantity !== item.lineTotalCents
        )
          return false;
        ids[item.productId] = true;
        total += item.lineTotalCents;
        count += item.quantity;
      }
      return (
        safeInteger(total, 0) &&
        total === data.totalCents &&
        count === data.cartCount
      );
    }
    function applySnapshot(data, request) {
      var byId = Object.create(null);
      data.items.forEach(function (item) {
        byId[item.productId] = item;
      });
      var unknown = data.items.some(function (item) {
        return !state.rows.some(function (row) {
          return row.id === item.productId;
        });
      });
      var removed = false;
      state.rows = state.rows.filter(function (row) {
        var item = byId[row.id];
        if (!item) {
          if (row.element.parentNode)
            row.element.parentNode.removeChild(row.element);
          removed = true;
          return false;
        }
        var retain =
          !unknown &&
          (row === request.row
            ? row.version > request.version
            : row.dirty ||
              row.rawInvalid ||
              row.version > request.versions[row.id]);
        row.confirmedQty = item.quantity;
        row.unitPrice = item.unitPriceCents;
        row.stock = item.stockQuantity;
        row.maximum = item.maxQuantity;
        row.available = item.available;
        row.lineTotalText = item.lineTotalText;
        if (!retain || !row.available) {
          row.desiredQty = item.quantity;
          row.rawInvalid = false;
          row.input.value = String(item.quantity);
          row.lastInput = row.input.value;
        }
        validateRow(row);
        return true;
      });
      state.snapshot = data;
      updateHeader(data.cartCount);
      if (unknown) {
        state.externalChange = true;
        freeze(
          "Giỏ hàng đã thay đổi ở trang khác. Vui lòng tải lại để kiểm tra.",
        );
        return;
      }
      state.notice =
        data.message ||
        (data.success
          ? "Đã cập nhật giỏ hàng."
          : "Chưa thể cập nhật số lượng. Vui lòng kiểm tra lại.");
      state.noticeKind = data.success ? "success" : "error";
      if (removed) {
        state.notice =
          "Một sản phẩm không còn trong giỏ hàng đã được cập nhật khỏi danh sách. " +
          state.notice;
      }
    }
    function schedule() {
      window.clearTimeout(state.timer);
      state.timer = null;
      if (state.running || state.frozen) return;
      var candidates = state.rows.filter(function (row) {
        return row.dirty && !row.invalid && row.available;
      });
      if (!candidates.length) return;
      var due = Math.min.apply(
        null,
        candidates.map(function (row) {
          return row.editedAt + 250;
        }),
      );
      state.timer = window.setTimeout(pump, Math.max(0, due - Date.now()));
    }
    function pump() {
      state.timer = null;
      if (state.running || state.frozen) return;
      var now = Date.now();
      var candidates = state.rows
        .filter(function (row) {
          return (
            row.dirty &&
            !row.invalid &&
            row.available &&
            row.editedAt + 250 <= now
          );
        })
        .sort(function (a, b) {
          return a.sequence - b.sequence;
        });
      if (!candidates.length) {
        schedule();
        return;
      }
      var row = candidates[0],
        body;
      try {
        body = new window.FormData(row.form);
        body.set("productId", String(row.id));
        body.set("quantity", String(row.desiredQty));
      } catch (ignore) {
        freeze(
          "Chưa thể gửi thay đổi số lượng. Vui lòng tải lại giỏ hàng để kiểm tra.",
        );
        return;
      }
      var request = {
        row: row,
        version: row.version,
        quantity: row.desiredQty,
        versions: Object.create(null),
      };
      state.rows.forEach(function (item) {
        request.versions[item.id] = item.version;
      });
      state.running = request;
      render();
      window.Promise.resolve()
        .then(function () {
          return window.fetch(row.url, {
            method: "POST",
            credentials: "same-origin",
            mode: "same-origin",
            redirect: "error",
            headers: {
              "X-Requested-With": "XMLHttpRequest",
              Accept: "application/json",
            },
            body: body,
          });
        })
        .then(function (response) {
          return response.text().then(function (bodyText) {
            var data = null;
            try {
              data = JSON.parse(bodyText);
            } catch (ignore) {}
            if (!response.ok || !validSnapshot(data)) {
              freeze(
                "Chưa xác nhận được số lượng đã lưu. Vui lòng tải lại giỏ hàng để kiểm tra trước khi tiếp tục.",
              );
              return;
            }
            applySnapshot(data, request);
          });
        })
        .catch(function () {
          freeze(
            "Chưa xác nhận được số lượng đã lưu. Vui lòng tải lại giỏ hàng để kiểm tra trước khi tiếp tục.",
          );
        })
        .then(
          function () {
            state.running = null;
            render();
            schedule();
          },
          function () {
            state.running = null;
            freeze(
              "Giỏ hàng chưa được xác nhận. Vui lòng tải lại để kiểm tra.",
            );
          },
        );
    }
    function edited(row) {
      if (state.frozen || !row.available) return;
      var raw = row.input.value;
      if (raw === row.lastInput) return;
      row.lastInput = raw;
      row.version += 1;
      row.sequence = ++state.sequence;
      row.editedAt = Date.now();
      var value = /^\d+$/.test(raw) ? Number(raw) : NaN;
      row.rawInvalid = !safeInteger(value, 1, 99);
      if (!row.rawInvalid) row.desiredQty = value;
      state.notice = "";
      state.noticeKind = "";
      validateRow(row);
      render();
      schedule();
    }

    root.addEventListener("input", function (event) {
      if (!event.target.hasAttribute("data-cart-quantity-input")) return;
      var row = findRow(event.target);
      if (row) edited(row);
    });
    root.addEventListener("change", function (event) {
      if (!event.target.hasAttribute("data-cart-quantity-input")) return;
      var row = findRow(event.target);
      if (row) edited(row);
    });
    root.addEventListener("click", function (event) {
      var checkout = event.target.closest("[data-cart-quantity-checkout]");
      if (checkout && root.contains(checkout) && checkoutBlocked()) {
        event.preventDefault();
        render();
        return;
      }
      var stepButton = event.target.closest("[data-cart-quantity-step]");
      if (!stepButton || !root.contains(stepButton)) return;
      var row = findRow(stepButton);
      if (!row || state.frozen || !row.available || stepButton.disabled) return;
      event.preventDefault();
      var raw = Number(row.input.value);
      var current = safeInteger(raw, 1, 99) ? raw : row.desiredQty;
      var step = Number(stepButton.getAttribute("data-cart-quantity-step"));
      var next = current + step;
      if (next < 1 || (step > 0 && next > row.maximum)) return;
      row.input.value = String(next);
      edited(row);
    });
    root.addEventListener("keydown", function (event) {
      var checkout = event.target.closest("[data-cart-quantity-checkout]");
      if (
        checkout &&
        checkoutBlocked() &&
        (event.key === "Enter" || event.key === " ")
      ) {
        event.preventDefault();
        render();
      }
    });
    root.addEventListener("submit", function (event) {
      var form = event.target;
      if (form.hasAttribute("data-cart-quantity-remove")) {
        if (state.frozen || busy()) {
          event.preventDefault();
          render();
        }
        return;
      }
      if (!form.hasAttribute("data-cart-quantity-form")) return;
      event.preventDefault();
      var row = findRow(form);
      if (!row || state.frozen) {
        render();
        return;
      }
      edited(row);
      if (row.invalid || !row.available) {
        render();
        if (typeof row.input.reportValidity === "function")
          row.input.reportValidity();
        return;
      }
      row.editedAt = Date.now() - 250;
      window.clearTimeout(state.timer);
      state.timer = null;
      pump();
    });

    root.classList.add("cart-quantity-ready");
    root.setAttribute("data-cart-quantity-ready", "true");
    Array.prototype.forEach.call(
      root.querySelectorAll("[data-cart-quantity-step]"),
      function (button) {
        button.hidden = false;
      },
    );
    render();
  }
  if (document.readyState === "loading")
    document.addEventListener("DOMContentLoaded", initialize);
  else initialize();
})();
