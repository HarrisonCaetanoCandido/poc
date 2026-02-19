import React, { useEffect, useState } from 'react'

const API = import.meta.env.VITE_API_URL || 'http://localhost:5000'

const LOGO = "https://cdn.prod.website-files.com/6357d4aeff33e738c1d2c99a/68caec177de208ed5ddc5cd2_Logo.png"

const statusConfig = {
  pending: { label: 'Pending', bg: '#fff8e1', color: '#f59e0b', dot: '#f59e0b' },
  processing: { label: 'Processing', bg: '#e8f4ff', color: '#66baff', dot: '#66baff' },
  completed: { label: 'Completed', bg: '#e6f9f0', color: '#10b981', dot: '#10b981' },
  // backend sends 'Finalized' — accept it as completed in the UI
  finalized: { label: 'Completed', bg: '#e6f9f0', color: '#10b981', dot: '#10b981' },
  cancelled: { label: 'Cancelled', bg: '#fef2f2', color: '#ef4444', dot: '#ef4444' },
}

function StatusBadge({ status }) {
  const cfg = statusConfig[status?.toLowerCase()] || statusConfig.pending
  return (
    <span className="status-badge" style={{ '--bg': cfg.bg, '--color': cfg.color, '--dot': cfg.dot }}>
      <span className="dot" />
      {cfg.label}
    </span>
  )
}

export default function App() {
  const [orders, setOrders] = useState([])
  const [form, setForm] = useState({ client: '', product: '', value: '' })
  const [loading, setLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function load() {
    setLoading(true)
    try {
      const res = await fetch(`${API}/orders`)
      const data = await res.json()
      setOrders(data)
    } catch (e) {
      console.error(e)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  async function submit(e) {
    e.preventDefault()
    setSubmitting(true)
    try {
      await fetch(`${API}/orders`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ client: form.client, product: form.product, value: parseFloat(form.value) })
      })
      setForm({ client: '', product: '', value: '' })
      await load()
    } finally {
      setSubmitting(false)
    }
  }

  const totalValue = orders.reduce((s, o) => s + (o.value || 0), 0)

  // helper to normalize backend status values for counts and labels
  function normalizeStatus(s) {
    const key = String(s ?? '').toLowerCase();
    if (key === 'finalized' || key === 'completed') return 'completed';
    if (key === 'processing') return 'processing';
    return 'pending';
  }

  const totalOrders = orders.length;
  const totalValueFormatted = totalValue;
  const pendingCount = orders.filter(o => normalizeStatus(o.status) === 'pending').length;
  const completedCount = orders.filter(o => normalizeStatus(o.status) === 'completed').length;

  return (
    <div className="app">
      {/* Header */}
      <header className="header">
        <img src={LOGO} alt="Logo" className="logo" />
        <span className="header-title">Order Management</span>
      </header>

      <main className="main">

        {/* Stats bar */}
        <div className="stats">
          {[
            { label: 'Total Orders', value: totalOrders },
            { label: 'Total Value', value: `R$ ${totalValueFormatted.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` },
            { label: 'Pending', value: pendingCount },
            { label: 'Completed', value: completedCount },
          ].map(stat => (
            <div key={stat.label} className="stat-card">
              <div className="stat-value">{stat.value}</div>
              <div className="stat-label">{stat.label}</div>
            </div>
          ))}
        </div>

        <div className="content">

          {/* Form */}
          <div className="form-card">
            <div className="form-header">
              <h2>New Order</h2>
              <p>Fill in the fields below</p>
            </div>

            <form onSubmit={submit} className="form">
              {[
                { key: 'client', label: 'Client', placeholder: 'Client name' },
                { key: 'product', label: 'Product', placeholder: 'Product name' },
                { key: 'value', label: 'Value (R$)', placeholder: '0.00', type: 'number' },
              ].map(field => (
                <div key={field.key}>
                  <label className="input-label">{field.label}</label>
                  <input
                    className="input"
                    type={field.type || 'text'}
                    placeholder={field.placeholder}
                    value={form[field.key]}
                    onChange={e => setForm({ ...form, [field.key]: e.target.value })}
                    required
                    step={field.key === 'value' ? '0.01' : undefined}
                  />
                </div>
              ))}

              <button type="submit" disabled={submitting} className="btn-primary">
                {submitting ? 'Creating…' : '+ Create Order'}
              </button>
            </form>
          </div>

          {/* Table */}
          <div className="table-card">
            <div className="table-header">
              <h2 style={{ fontSize: 15, fontWeight: 700, margin: 0 }}>Orders</h2>
              <button onClick={load} className="refresh-btn">↻ Refresh</button>
            </div>

            {loading ? (
              <div className="table-loading">Loading orders…</div>
            ) : orders.length === 0 ? (
              <div className="table-empty">
                <div style={{ fontSize: 32, marginBottom: 8 }}>📋</div>
                No orders yet. Create one!
              </div>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="orders-table">
                  <thead>
                    <tr>
                      {['ID', 'Client', 'Product', 'Value', 'Status', 'Created At'].map(h => (
                        <th key={h}>{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {orders.map((o, i) => (
                      <tr key={o.id} className="order-row">
                        <td className="id-cell">{String(o.id).slice(0, 8)}…</td>
                        <td className="client-cell">{o.client}</td>
                        <td style={{ color: '#444' }}>{o.product}</td>
                        <td className="value-cell">R$ {Number(o.value).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</td>
                        <td><StatusBadge status={o.status} /></td>
                        <td className="date-cell">{new Date(o.createdAt).toLocaleString('pt-BR')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  )
}