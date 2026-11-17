"use strict";
document.addEventListener("DOMContentLoaded", function () {
  var toggle = document.querySelector(".menu-toggle");
  var nav = document.getElementById("main-navigation");
  if (toggle && nav)
    toggle.addEventListener("click", function () {
      var open = toggle.getAttribute("aria-expanded") === "true";
      toggle.setAttribute("aria-expanded", String(!open));
      nav.classList.toggle("is-open", !open);
    });
  document.querySelectorAll("[data-quantity]").forEach(function (button) {
    button.addEventListener("click", function () {
      var input = button.parentElement.querySelector("input");
      var min = parseInt(input.min, 10) || 1;
      var max = parseInt(input.max, 10) || 999;
      var current = parseInt(input.value, 10) || min;
      input.value = Math.max(
        min,
        Math.min(max, current + (button.dataset.quantity === "plus" ? 1 : -1)),
      );
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
  });
});
